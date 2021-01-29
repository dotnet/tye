// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class DockerRunner : IApplicationProcessor
    {
        private const string DockerReplicaStore = "docker";

        private static readonly TimeSpan DockerStopTimeout = TimeSpan.FromSeconds(30);
        private readonly ILogger _logger;

        private readonly ReplicaRegistry _replicaRegistry;

        public DockerRunner(ILogger logger, ReplicaRegistry replicaRegistry)
        {
            _logger = logger;
            _replicaRegistry = replicaRegistry;
        }

        public async Task StartAsync(Application application)
        {
            await PurgeFromPreviousRun();

            var containers = new List<Service>();

            foreach (var s in application.Services)
            {
                if (s.Value.Description.RunInfo is DockerRunInfo)
                {
                    containers.Add(s.Value);
                }
            }

            if (containers.Count == 0)
            {
                return;
            }

            var proxies = new List<Service>();
            foreach (var service in application.Services.Values)
            {
                if (service.Description.RunInfo is DockerRunInfo ||
                    service.Description.RunInfo is IngressRunInfo ||
                    service.Description.Bindings.Count == 0)
                {
                    continue;
                }

                // Inject a proxy per non-container service. This allows the container to use normal host names within the
                // container network to talk to services on the host
                var proxyContainer = new DockerRunInfo($"mcr.microsoft.com/dotnet/core/sdk:3.1", "dotnet Microsoft.Tye.Proxy.dll")
                {
                    WorkingDirectory = "/app",
                    NetworkAlias = service.Description.Name,
                    Private = true,
                    IsProxy = true
                };
                var proxyLocation = Path.GetDirectoryName(typeof(Microsoft.Tye.Proxy.Program).Assembly.Location);
                proxyContainer.VolumeMappings.Add(new DockerVolume(proxyLocation, name: null, target: "/app"));
                var proxyDescription = new ServiceDescription($"{service.Description.Name}-proxy", proxyContainer);
                foreach (var binding in service.Description.Bindings)
                {
                    if (binding.Port == null)
                    {
                        continue;
                    }

                    var b = new ServiceBinding()
                    {
                        ConnectionString = binding.ConnectionString,
                        Host = binding.Host,
                        ContainerPort = binding.ContainerPort,
                        Name = binding.Name,
                        Port = binding.Port,
                        Protocol = binding.Protocol
                    };
                    b.ReplicaPorts.Add(b.Port.Value);
                    proxyDescription.Bindings.Add(b);
                }
                var proxyContainerService = new Service(proxyDescription);
                containers.Add(proxyContainerService);
                proxies.Add(proxyContainerService);
            }

            string? dockerNetwork = null;

            if (!string.IsNullOrEmpty(application.Network))
            {
                var dockerNetworkResult = await ProcessUtil.RunAsync("docker", $"network ls --filter \"name={application.Network}\" --format \"{{{{.ID}}}}\"", throwOnError: false);
                if (dockerNetworkResult.ExitCode != 0)
                {
                    _logger.LogError("{Network}: Run docker network ls command failed", application.Network);

                    throw new CommandException("Run docker network ls command failed");
                }

                if (!string.IsNullOrWhiteSpace(dockerNetworkResult.StandardOutput))
                {
                    _logger.LogInformation("The specified network {Network} exists", application.Network);

                    dockerNetwork = application.Network;
                }
                else
                {
                    _logger.LogWarning("The specified network {Network} doesn't exist.", application.Network);

                    application.Network = null;
                }
            }

            // We're going to be making containers, only make a network if we have more than one (we assume they'll need to talk)
            if (string.IsNullOrEmpty(dockerNetwork) && containers.Count > 1)
            {
                dockerNetwork = "tye_network_" + Guid.NewGuid().ToString().Substring(0, 10);

                application.Items["dockerNetwork"] = dockerNetwork;

                _logger.LogInformation("Creating docker network {Network}", dockerNetwork);

                var command = $"network create --driver bridge {dockerNetwork}";

                _logger.LogInformation("Running docker command {Command}", command);

                var dockerNetworkResult = await ProcessUtil.RunAsync("docker", command, throwOnError: false);

                if (dockerNetworkResult.ExitCode != 0)
                {
                    _logger.LogInformation("Running docker command with exception info {ExceptionStdOut} {ExceptionStdErr}", dockerNetworkResult.StandardOutput, dockerNetworkResult.StandardError);

                    throw new CommandException("Run docker network create command failed");
                }
            }

            // Stash information outside of the application services
            application.Items[typeof(DockerApplicationInformation)] = new DockerApplicationInformation(dockerNetwork, proxies);

            var tasks = new Task[containers.Count];
            var index = 0;

            foreach (var s in containers)
            {
                var docker = (DockerRunInfo)s.Description.RunInfo!;

                tasks[index++] = StartContainerAsync(application, s, docker, dockerNetwork);
            }

            await Task.WhenAll(tasks);
        }

        public async Task StopAsync(Application application)
        {
            if (!application.Items.TryGetValue(typeof(DockerApplicationInformation), out var value))
            {
                return;
            }

            var info = (DockerApplicationInformation)value;

            var services = application.Services;

            var index = 0;
            var tasks = new Task[services.Count + info.Proxies.Count];
            foreach (var s in services.Values)
            {
                tasks[index++] = StopContainerAsync(s);
            }

            foreach (var s in info.Proxies)
            {
                tasks[index++] = StopContainerAsync(s);
            }

            await Task.WhenAll(tasks);

            if (string.IsNullOrEmpty(application.Network) && !string.IsNullOrEmpty(info.DockerNetwork))
            {
                _logger.LogInformation("Removing docker network {Network}", info.DockerNetwork);

                var command = $"network rm {info.DockerNetwork}";

                _logger.LogInformation("Running docker command {Command}", command);

                // Clean up the network we created
                await ProcessUtil.RunAsync("docker", command, throwOnError: false);
            }
        }

        private async Task StartContainerAsync(Application application, Service service, DockerRunInfo docker, string? dockerNetwork)
        {
            var serviceDescription = service.Description;
            var environmentArguments = "";
            var volumes = "";
            var workingDirectory = docker.WorkingDirectory != null ? $"-w \"{docker.WorkingDirectory}\"" : "";
            var hostname = "host.docker.internal";
            var dockerImage = docker.Image ?? service.Description.Name;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // See: https://github.com/docker/for-linux/issues/264
                //
                // host.docker.internal is making it's way into linux docker but doesn't work yet
                // instead we use the machine IP
                var addresses = await Dns.GetHostAddressesAsync(Dns.GetHostName());
                hostname = addresses[0].ToString();
            }

            async Task RunDockerContainer(IEnumerable<(int ExternalPort, int Port, int? ContainerPort, string? Protocol)> ports, CancellationToken cancellationToken)
            {
                var hasPorts = ports.Any();

                var replica = service.Description.Name.ToLower() + "_" + Guid.NewGuid().ToString().Substring(0, 10).ToLower();
                var status = new DockerStatus(service, replica);
                service.Replicas[replica] = status;

                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Added, status));

                var environment = new Dictionary<string, string>();

                var portString = "";

                if (hasPorts)
                {
                    status.Ports = ports.Select(p => p.Port);
                    status.Bindings = ports.Select(p => new ReplicaBinding() { Port = p.Port, ExternalPort = p.ExternalPort, Protocol = p.Protocol }).ToList();

                    // These are the ports that the application should use for binding

                    // 1. Tell the docker container what port to bind to
                    portString = docker.Private ? "" : string.Join(" ", ports.Select(p => $"-p {p.Port}:{p.ContainerPort ?? p.Port}{(string.Equals(p.Protocol, "udp", StringComparison.OrdinalIgnoreCase) ? "/udp" : string.Empty)}"));

                    if (docker.IsAspNet)
                    {
                        // 2. Configure ASP.NET Core to bind to those same ports
                        environment["ASPNETCORE_URLS"] = string.Join(";", ports.Select(p => $"{p.Protocol ?? "http"}://*:{p.ContainerPort ?? p.Port}"));

                        // Set the HTTPS port for the redirect middleware
                        foreach (var p in ports)
                        {
                            if (string.Equals(p.Protocol, "https", StringComparison.OrdinalIgnoreCase))
                            {
                                // We need to set the redirect URL to the exposed port so the redirect works cleanly
                                environment["HTTPS_PORT"] = p.ExternalPort.ToString();
                            }
                        }
                    }

                    // 3. For non-ASP.NET Core apps, pass the same information in the PORT env variable as a semicolon separated list.
                    environment["PORT"] = string.Join(";", ports.Select(p => $"{p.ContainerPort ?? p.Port}"));

                    // This the port for the container proxy (containerport:externalport)
                    environment["PROXY_PORT"] = string.Join(";", ports.Select(p => $"{p.ContainerPort ?? p.Port}:{p.ExternalPort}"));
                }

                // See: https://github.com/docker/for-linux/issues/264
                //
                // The way we do proxying here doesn't really work for multi-container scenarios on linux
                // without some more setup.
                application.PopulateEnvironment(service, (key, value) => environment[key] = value, hostname!);

                environment["APP_INSTANCE"] = replica;
                environment["CONTAINER_HOST"] = hostname!;

                status.Environment = environment;

                foreach (var pair in environment)
                {
                    environmentArguments += $"-e \"{pair.Key}={pair.Value}\" ";
                }

                foreach (var volumeMapping in docker.VolumeMappings)
                {
                    if (volumeMapping.Source != null)
                    {
                        var sourcePath = Path.GetFullPath(Path.Combine(application.ContextDirectory, volumeMapping.Source));
                        volumes += $"-v \"{sourcePath}:{volumeMapping.Target}\" ";
                    }
                    else if (volumeMapping.Name != null)
                    {
                        volumes += $"-v \"{volumeMapping.Name}:{volumeMapping.Target}\" ";
                    }
                }

                var command = $"run -d {workingDirectory} {volumes} {environmentArguments} {portString} --name {replica} --restart=unless-stopped {dockerImage} {docker.Args ?? ""}";

                if (!docker.IsProxy)
                {
                    _logger.LogInformation("Running image {Image} for {Replica}", docker.Image, replica);
                }
                else
                {
                    _logger.LogDebug("Running proxy image {Image} for {Replica}", docker.Image, replica);
                }

                service.Logs.OnNext($"[{replica}]: docker {command}");

                status.DockerCommand = command;
                status.DockerNetwork = dockerNetwork;

                WriteReplicaToStore(replica);
                var result = await ProcessUtil.RunAsync(
                    "docker",
                    command,
                    throwOnError: false,
                    cancellationToken: cancellationToken,
                    outputDataReceived: data => service.Logs.OnNext($"[{replica}]: {data}"),
                    errorDataReceived: data => service.Logs.OnNext($"[{replica}]: {data}"));

                if (result.ExitCode != 0)
                {
                    _logger.LogError("docker run failed for {ServiceName} with exit code {ExitCode}:" + result.StandardError, service.Description.Name, result.ExitCode);
                    service.Replicas.TryRemove(replica, out var _);
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

                if (!string.IsNullOrEmpty(dockerNetwork))
                {
                    status.DockerNetworkAlias = docker.NetworkAlias ?? serviceDescription!.Name;

                    var networkCommand = $"network connect {dockerNetwork} {replica} --alias {status.DockerNetworkAlias}";

                    service.Logs.OnNext($"[{replica}]: docker {networkCommand}");

                    _logger.LogInformation("Running docker command {Command}", networkCommand);

                    result = await ProcessUtil.RunAsync("docker", networkCommand);

                    PrintStdOutAndErr(service, replica, result);
                }

                var sentStartedEvent = false;

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (sentStartedEvent)
                    {
                        using var restartCts = new CancellationTokenSource(DockerStopTimeout);
                        result = await ProcessUtil.RunAsync("docker", $"restart {containerId}", throwOnError: false, cancellationToken: restartCts.Token);

                        if (restartCts.IsCancellationRequested)
                        {
                            _logger.LogWarning($"Failed to restart container after {DockerStopTimeout.Seconds} seconds.", replica, shortContainerId);
                            break; // implement retry mechanism?
                        }
                        else if (result.ExitCode != 0)
                        {
                            _logger.LogWarning($"Failed to restart container due to exit code {result.ExitCode}.", replica, shortContainerId);
                            break;
                        }

                        service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Stopped, status));
                    }

                    using var stoppingCts = new CancellationTokenSource();
                    status.StoppingTokenSource = stoppingCts;
                    service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Started, status));
                    sentStartedEvent = true;

                    await using var _ = cancellationToken.Register(() => status.StoppingTokenSource.Cancel());

                    _logger.LogInformation("Collecting docker logs for {ContainerName}.", replica);

                    var backOff = TimeSpan.FromSeconds(5);

                    while (!status.StoppingTokenSource.Token.IsCancellationRequested)
                    {
                        var logsRes = await ProcessUtil.RunAsync("docker", $"logs -f {containerId}",
                            outputDataReceived: data => service.Logs.OnNext($"[{replica}]: {data}"),
                            errorDataReceived: data => service.Logs.OnNext($"[{replica}]: {data}"),
                            throwOnError: false,
                            cancellationToken: status.StoppingTokenSource.Token);

                        if (logsRes.ExitCode != 0)
                        {
                            break;
                        }

                        if (!status.StoppingTokenSource.IsCancellationRequested)
                        {
                            try
                            {
                                // Avoid spamming logs if restarts are happening
                                await Task.Delay(backOff, status.StoppingTokenSource.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                        }

                        backOff *= 2;
                    }

                    _logger.LogInformation("docker logs collection for {ContainerName} complete with exit code {ExitCode}", replica, result.ExitCode);

                    status.StoppingTokenSource = null;
                }

                // Docker has a tendency to get stuck so we're going to timeout this shutdown process
                var timeoutCts = new CancellationTokenSource(DockerStopTimeout);

                _logger.LogInformation("Stopping container {ContainerName} with ID {ContainerId}", replica, shortContainerId);

                result = await ProcessUtil.RunAsync("docker", $"stop {containerId}", throwOnError: false, cancellationToken: timeoutCts.Token);

                if (timeoutCts.IsCancellationRequested)
                {
                    _logger.LogWarning($"Failed to stop container after {DockerStopTimeout.Seconds} seconds, container will most likely be running.", replica, shortContainerId);
                }

                PrintStdOutAndErr(service, replica, result);

                if (sentStartedEvent)
                {
                    service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Stopped, status));
                }

                _logger.LogInformation("Stopped container {ContainerName} with ID {ContainerId} exited with {ExitCode}", replica, shortContainerId, result.ExitCode);

                result = await ProcessUtil.RunAsync("docker", $"rm {containerId}", throwOnError: false, cancellationToken: timeoutCts.Token);

                if (timeoutCts.IsCancellationRequested)
                {
                    _logger.LogWarning($"Failed to remove container after {DockerStopTimeout.Seconds} seconds, container will most likely still exist.", replica, shortContainerId);
                }

                PrintStdOutAndErr(service, replica, result);

                _logger.LogInformation("Removed container {ContainerName} with ID {ContainerId} exited with {ExitCode}", replica, shortContainerId, result.ExitCode);

                service.Replicas.TryRemove(replica, out var _);

                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Removed, status));
            };

            async Task DockerBuildAsync(CancellationToken cancellationToken)
            {
                if (docker.DockerFile != null)
                {
                    _logger.LogInformation("Building docker image {Image} from docker file", dockerImage);

                    void Log(string data)
                    {
                        _logger.LogInformation("[" + serviceDescription!.Name + "]:" + data);
                        service.Logs.OnNext(data);
                    }

                    var arguments = new StringBuilder($"build \"{docker.DockerFileContext?.FullName}\" -t {dockerImage} -f \"{docker.DockerFile}\"");

                    foreach (var buildArg in docker.BuildArgs)
                    {
                        arguments.Append($" --build-arg {buildArg.Key}={buildArg.Value}");
                    }

                    var dockerBuildResult = await ProcessUtil.RunAsync(
                        $"docker",
                        arguments.ToString(),
                        outputDataReceived: Log,
                        errorDataReceived: Log,
                        workingDirectory: docker.WorkingDirectory,
                        cancellationToken: cancellationToken,
                        throwOnError: false);

                    if (dockerBuildResult.ExitCode != 0)
                    {
                        throw new CommandException("'docker build' failed.");
                    }
                }
            }

            Task DockerRunAsync(CancellationToken cancellationToken)
            {
                var tasks = new Task[serviceDescription!.Replicas];

                if (serviceDescription.Bindings.Count > 0)
                {
                    // Each replica is assigned a list of internal ports, one mapped to each external
                    // port
                    for (var i = 0; i < serviceDescription.Replicas; i++)
                    {
                        var ports = new List<(int, int, int?, string?)>();
                        foreach (var binding in serviceDescription.Bindings)
                        {
                            if (binding.Port == null)
                            {
                                continue;
                            }

                            ports.Add((binding.Port.Value, binding.ReplicaPorts[i], binding.ContainerPort, binding.Protocol));
                        }

                        tasks[i] = RunDockerContainer(ports, cancellationToken);
                    }
                }
                else
                {
                    for (var i = 0; i < service.Description.Replicas; i++)
                    {
                        tasks[i] = RunDockerContainer(Enumerable.Empty<(int, int, int?, string?)>(), cancellationToken);
                    }
                }

                return Task.WhenAll(tasks);
            }

            async Task BuildAndRunAsync(CancellationToken cancellationToken)
            {
                await DockerBuildAsync(cancellationToken);

                await DockerRunAsync(cancellationToken);
            }

            var dockerInfo = new DockerInformation();
            dockerInfo.Task = BuildAndRunAsync(dockerInfo.StoppingTokenSource.Token);

            service.Items[typeof(DockerInformation)] = dockerInfo;
        }

        private async Task PurgeFromPreviousRun()
        {
            var dockerReplicas = await _replicaRegistry.GetEvents(DockerReplicaStore);
            foreach (var replica in dockerReplicas)
            {
                var container = replica["container"];
                await ProcessUtil.RunAsync("docker", $"rm -f {container}", throwOnError: false);
                _logger.LogInformation("removed container {container} from previous run", container);
            }

            _replicaRegistry.DeleteStore(DockerReplicaStore);
        }

        private void WriteReplicaToStore(string container)
        {
            _replicaRegistry.WriteReplicaEvent(DockerReplicaStore, new Dictionary<string, string>()
            {
                ["container"] = container
            });
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

        private Task StopContainerAsync(Service service)
        {
            if (service.Items.TryGetValue(typeof(DockerInformation), out var value) && value is DockerInformation di)
            {
                di.StoppingTokenSource.Cancel();

                return di.Task;
            }

            return Task.CompletedTask;
        }

        private class DockerInformation
        {
            public Task Task { get; set; } = default!;
            public CancellationTokenSource StoppingTokenSource { get; } = new CancellationTokenSource();
        }

        private class DockerApplicationInformation
        {
            public DockerApplicationInformation(string? dockerNetwork, List<Service> proxies)
            {
                DockerNetwork = dockerNetwork;
                Proxies = proxies;
            }

            public string? DockerNetwork { get; set; }

            public List<Service> Proxies { get; }
        }
    }
}
