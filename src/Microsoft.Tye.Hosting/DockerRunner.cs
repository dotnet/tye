// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class DockerRunner : IApplicationProcessor
    {
        private readonly ILogger _logger;

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
            if (!await DockerDetector.Instance.IsDockerInstalled.Value)
            {
                _logger.LogError("Unable to start docker container for service {ServiceName}, Docker is not installed.", service.Description.Name);

                service.Logs.OnNext($"Unable to start docker container for service {service.Description.Name}, Docker is not installed.");
                return;
            }

            if (!await DockerDetector.Instance.IsDockerConnectedToDaemon.Value)
            {
                _logger.LogError("Unable to start docker container for service {ServiceName}, Docker is not running.", service.Description.Name);

                service.Logs.OnNext($"Unable to start docker container for service {service.Description.Name}, Docker is not running.");
                return;
            }

            var serviceDescription = service.Description;
            var environmentArguments = "";
            var volumes = "";
            var workingDirectory = docker.WorkingDirectory != null ? $"-w {docker.WorkingDirectory}" : "";

            // This is .NET specific
            var userSecretStore = GetUserSecretsPathFromSecrets();

            if (!string.IsNullOrEmpty(userSecretStore))
            {
                // Map the user secrets on this drive to user secrets
                docker.VolumeMappings[userSecretStore] = "/root/.microsoft/usersecrets:ro";
            }

            var dockerInfo = new DockerInformation(new Task[service.Description.Replicas]);

            async Task RunDockerContainer(IEnumerable<(int Port, int? ContainerPort, int BindingPort, string? Protocol)> ports)
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

                    // These are the ports that the application should use for binding

                    // 1. Tell the docker container what port to bind to
                    portString = string.Join(" ", ports.Select(p => $"-p {p.Port}:{p.ContainerPort ?? p.Port}"));

                    // 2. Configure ASP.NET Core to bind to those same ports
                    environment["ASPNETCORE_URLS"] = string.Join(";", ports.Select(p => $"{p.Protocol ?? "http"}://*:{p.ContainerPort ?? p.Port}"));

                    // Set the HTTPS port for the redirect middleware
                    foreach (var p in ports)
                    {
                        if (string.Equals(p.Protocol, "https", StringComparison.OrdinalIgnoreCase))
                        {
                            // We need to set the redirect URL to the exposed port so the redirect works cleanly
                            environment["HTTPS_PORT"] = p.BindingPort.ToString();
                        }
                    }

                    // 3. For non-ASP.NET Core apps, pass the same information in the PORT env variable as a semicolon separated list.
                    environment["PORT"] = string.Join(";", ports.Select(p => $"{p.ContainerPort ?? p.Port}"));
                }

                // See: https://github.com/docker/for-linux/issues/264
                //
                // The way we do proxying here doesn't really work for multi-container scenarios on linux
                // without some more setup.
                application.PopulateEnvironment(service, (key, value) => environment[key] = value, "host.docker.internal");

                environment["APP_INSTANCE"] = replica;

                foreach (var pair in environment)
                {
                    environmentArguments += $"-e {pair.Key}={pair.Value} ";
                }

                foreach (var pair in docker.VolumeMappings)
                {
                    var sourcePath = Path.GetFullPath(Path.Combine(application.ContextDirectory, pair.Key.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)));
                    volumes += $"-v {sourcePath}:{pair.Value} ";
                }

                var command = $"run -d {workingDirectory} {volumes} {environmentArguments} {portString} --name {replica} --restart=unless-stopped {docker.Image} {docker.Args ?? ""}";
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

                // Docker has a tendency to get stuck so we're going to timeout this shutdown process
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

                        ports.Add((service.PortMap[binding.Port.Value][i], binding.ContainerPort, binding.Port.Value, binding.Protocol));
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

        private static string? GetUserSecretsPathFromSecrets()
        {
            // This is the logic used to determine the user secrets path
            // See https://github.com/dotnet/extensions/blob/64140f90157fec1bfd8aeafdffe8f30308ccdf41/src/Configuration/Config.UserSecrets/src/PathHelper.cs#L27
            const string userSecretsFallbackDir = "DOTNET_USER_SECRETS_FALLBACK_DIR";

            // For backwards compat, this checks env vars first before using Env.GetFolderPath
            var appData = Environment.GetEnvironmentVariable("APPDATA");
            var root = appData                                                                   // On Windows it goes to %APPDATA%\Microsoft\UserSecrets\
                       ?? Environment.GetEnvironmentVariable("HOME")                             // On Mac/Linux it goes to ~/.microsoft/usersecrets/
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                       ?? Environment.GetEnvironmentVariable(userSecretsFallbackDir);            // this fallback is an escape hatch if everything else fails

            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            return !string.IsNullOrEmpty(appData)
                ? Path.Combine(root, "Microsoft", "UserSecrets")
                : Path.Combine(root, ".microsoft", "usersecrets");
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
