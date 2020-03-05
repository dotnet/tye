// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tye.Hosting.Model;
using Microsoft.Extensions.Logging;

namespace Tye.Hosting
{
    public class DockerRunner : IApplicationProcessor
    {
        private readonly ILogger _logger;
        private readonly Lazy<Task<bool>> _dockerInstalled = new Lazy<Task<bool>>(DetectDockerInstalled);

        public DockerRunner(ILogger logger)
        {
            _logger = logger;
        }

        public Task StartAsync(Application application)
        {
            var tasks = new Task[application.Services.Count];
            var index = 0;
            foreach (var s in application.Services)
            {
                tasks[index++] = s.Value.Description.RunInfo is DockerRunInfo docker ? StartContainerAsync(application, s.Value, docker) : Task.CompletedTask;
            }

            return Task.WhenAll(tasks);
        }

        public Task StopAsync(Application application)
        {
            var services = application.Services;

            var index = 0;
            var tasks = new Task[services.Count];
            foreach (var s in services.Values)
            {
                var state = s;
                tasks[index++] = StopContainerAsync(state);
            }

            return Task.WhenAll(tasks);
        }

        private async Task StartContainerAsync(Application application, Service service, DockerRunInfo docker)
        {
            if (!await _dockerInstalled.Value)
            {
                _logger.LogError("Unable to start docker container for service {ServiceName}, Docker is not installed.", service.Description.Name);

                service.Logs.OnNext($"Unable to start docker container for service {service.Description.Name}, Docker is not installed.");
                return;
            }

            var serviceDescription = service.Description;
            var environmentArguments = "";

            var dockerInfo = new DockerInformation(new Task[service.Description.Replicas]);

            async Task RunDockerContainer(IEnumerable<(int Port, int? InternalPort, int BindingPort, string? Protocol)> ports)
            {
                var hasPorts = ports.Any();

                var replica = service.Description.Name.ToLower() + "_" + Guid.NewGuid().ToString().Substring(0, 10).ToLower();
                var status = new DockerStatus(service, replica);
                service.Replicas[replica] = status;

                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Added, status));

                var environment = new Dictionary<string, string>
                {
                    // Default to development environment
                    ["DOTNET_ENVIRONMENT"] = "Development",
                    // Remove the color codes from the console output
                    ["DOTNET_LOGGING__CONSOLE__DISABLECOLORS"] = "true"
                };

                var portString = "";

                if (hasPorts)
                {
                    status.Ports = ports.Select(p => p.Port);

                    portString = string.Join(" ", ports.Select(p => $"-p {p.Port}:{p.InternalPort ?? p.Port}"));

                    foreach (var p in ports)
                    {
                        environment[$"{p.Protocol?.ToUpper() ?? "HTTP"}_PORT"] = p.BindingPort.ToString();
                    }
                }

                application.PopulateEnvironment(service, (key, value) => environment[key] = value, "host.docker.internal");

                environment["APP_INSTANCE"] = replica;

                foreach (var pair in environment)
                {
                    environmentArguments += $"-e {pair.Key}={pair.Value} ";
                }

                var command = $"run -d {environmentArguments} {portString} --name {replica} --restart=unless-stopped {docker.Image} {docker.Args ?? ""}";
                _logger.LogInformation("Running docker command {Command}", command);

                service.Logs.OnNext($"[{replica}]: {command}");

                status.DockerCommand = command;

                var result = await ProcessUtil.RunAsync(
                    "docker",
                    command,
                    throwOnError: false,
                    cancellationToken: dockerInfo.StoppingTokenSource.Token,
                    outputDataReceived: data => service.Logs.OnNext($"[{replica}]: {data}"));

                if (result.ExitCode != 0)
                {
                    _logger.LogError("docker run failed for {ServiceName} with exit code {ExitCode}:" + result.StandardError, service.Description.Name, result.ExitCode);
                    service.Replicas.TryRemove(replica, out _);
                    service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Removed, status));

                    PrintStdOutAndErr(service, replica, result);
                    return;
                }

                var containerId = (string?)result.StandardOutput.Trim();

                // There's a race condition that sometimes makes us miss the output
                // so keep trying to get the container id
                while (string.IsNullOrEmpty(containerId))
                {
                    // Try to get the ID of the container
                    result = await ProcessUtil.RunAsync("docker", $"ps --no-trunc -f name={replica} --format " + "{{.ID}}");

                    containerId = result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
                }

                var shortContainerId = containerId.Substring(0, Math.Min(12, containerId.Length));

                status.ContainerId = shortContainerId;

                _logger.LogInformation("Running container {ContainerName} with ID {ContainerId}", replica, shortContainerId);

                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Started, status));

                _logger.LogInformation("Collecting docker logs for {ContainerName}.", replica);

                await ProcessUtil.RunAsync("docker", $"logs -f {containerId}",
                    outputDataReceived: data => service.Logs.OnNext($"[{replica}]: {data}"),
                    onStart: pid =>
                    {
                        status.DockerLogsPid = pid;
                    },
                    throwOnError: false,
                    cancellationToken: dockerInfo.StoppingTokenSource.Token);

                _logger.LogInformation("docker logs collection for {ContainerName} complete with exit code {ExitCode}", replica, result.ExitCode);

                // Docker has a tendency to hang so we're going to timeout this shutdown process
                var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                _logger.LogInformation("Stopping container {ContainerName} with ID {ContainerId}", replica, shortContainerId);

                result = await ProcessUtil.RunAsync("docker", $"stop {containerId}", throwOnError: false, cancellationToken: timeoutCts.Token);

                PrintStdOutAndErr(service, replica, result);

                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Stopped, status));

                _logger.LogInformation("Stopped container {ContainerName} with ID {ContainerId} exited with {ExitCode}", replica, shortContainerId, result.ExitCode);

                result = await ProcessUtil.RunAsync("docker", $"rm {containerId}", throwOnError: false, cancellationToken: timeoutCts.Token);

                PrintStdOutAndErr(service, replica, result);

                _logger.LogInformation("Removed container {ContainerName} with ID {ContainerId} exited with {ExitCode}", replica, shortContainerId, result.ExitCode);

                service.Replicas.TryRemove(replica, out _);

                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Removed, status));
            };

            if (serviceDescription.Bindings.Count > 0)
            {
                // Each replica is assigned a list of internal ports, one mapped to each external
                // port
                for (var i = 0; i < serviceDescription.Replicas; i++)
                {
                    var ports = new List<(int, int?, int, string?)>();
                    foreach (var binding in serviceDescription.Bindings)
                    {
                        if (binding.Port == null)
                        {
                            continue;
                        }

                        ports.Add((service.PortMap[binding.Port.Value][i], binding.InternalPort, binding.Port.Value, binding.Protocol));
                    }

                    dockerInfo.Tasks[i] = RunDockerContainer(ports);
                }
            }
            else
            {
                for (var i = 0; i < service.Description.Replicas; i++)
                {
                    dockerInfo.Tasks[i] = RunDockerContainer(Enumerable.Empty<(int, int?, int, string?)>());
                }
            }

            service.Items[typeof(DockerInformation)] = dockerInfo;
        }

        private static void PrintStdOutAndErr(Service service, string replica, ProcessResult result)
        {
            if (result.ExitCode != 0)
            {
                if (result.StandardOutput != null)
                {
                    service.Logs.OnNext($"[{replica}]: {result.StandardOutput}");
                }

                if (result.StandardError != null)
                {
                    service.Logs.OnNext($"[{replica}]: {result.StandardError}");
                }
            }
        }

        private async Task StopContainerAsync(Service service)
        {
            if (service.Items.TryGetValue(typeof(DockerInformation), out var value) && value is DockerInformation di)
            {
                di.StoppingTokenSource.Cancel();

                await Task.WhenAll(di.Tasks);
            }
        }

        private static async Task<bool> DetectDockerInstalled()
        {
            // Detect Docker installation
            try
            {
                await ProcessUtil.RunAsync("docker", "version", throwOnError: false);
                return true;
            }
            catch (Exception)
            {
                // Unfortunately, process throws
                return false;
            }
        }


        private class DockerInformation
        {
            public DockerInformation(Task[] tasks)
            {
                Tasks = tasks;
            }

            public Task[] Tasks { get; }
            public CancellationTokenSource StoppingTokenSource { get; } = new CancellationTokenSource();
        }
    }
}
