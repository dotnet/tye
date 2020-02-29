using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Micronetes.Hosting.Model;
using Microsoft.Extensions.Logging;

namespace Micronetes.Hosting
{
    public class ProcessRunner : IApplicationProcessor
    {
        private readonly ILogger _logger;
        private readonly bool _debugMode;
        private readonly bool _buildProjects;

        public ProcessRunner(ILogger logger, ProcessRunnerOptions options)
        {
            _logger = logger;
            _debugMode = options.DebugMode;
            _buildProjects = options.BuildProjects;
        }

        public Task StartAsync(Application application)
        {
            var tasks = new Task[application.Services.Count];
            var index = 0;
            foreach (var s in application.Services)
            {
                tasks[index++] = s.Value.Description.External ? Task.CompletedTask : LaunchService(application, s.Value);
            }

            return Task.WhenAll(tasks);
        }

        public Task StopAsync(Application application)
        {
            return KillRunningProcesses(application.Services);
        }

        private async Task LaunchService(Application application, Service service)
        {
            var serviceDescription = service.Description;

            if (serviceDescription.DockerImage != null)
            {
                return;
            }

            var serviceName = serviceDescription.Name;

            var path = "";
            var workingDirectory = "";
            var args = service.Description.Args ?? "";

            if (serviceDescription.Project != null)
            {
                var expandedProject = Environment.ExpandEnvironmentVariables(serviceDescription.Project);
                var fullProjectPath = Path.GetFullPath(Path.Combine(application.ContextDirectory, expandedProject));
                path = GetExePath(fullProjectPath);
                workingDirectory = Path.GetDirectoryName(fullProjectPath);
                service.Status.ProjectFilePath = fullProjectPath;
            }
            else
            {
                var expandedExecutable = Environment.ExpandEnvironmentVariables(serviceDescription.Executable);
                path = Path.GetFullPath(Path.Combine(application.ContextDirectory, expandedExecutable));
                workingDirectory = serviceDescription.WorkingDirectory != null ?
                    Path.GetFullPath(Path.Combine(application.ContextDirectory, Environment.ExpandEnvironmentVariables(serviceDescription.WorkingDirectory))) :
                    Path.GetDirectoryName(path);
            }

            // If this is a dll then use dotnet to run it
            if (Path.GetExtension(path) == ".dll")
            {
                args = $"\"{path}\" {args}".Trim();
                path = "dotnet";
            }

            service.Status.ExecutablePath = path;
            service.Status.WorkingDirectory = workingDirectory;
            service.Status.Args = args;

            var processInfo = new ProcessInfo
            {
                Tasks = new Task[service.Description.Replicas.Value]
            };

            if (service.Status.ProjectFilePath != null && service.Description.Build.GetValueOrDefault() && _buildProjects)
            {
                // Sometimes building can fail because of file locking (like files being open in VS)
                _logger.LogInformation("Building project {ProjectFile}", service.Status.ProjectFilePath);

                service.Logs.OnNext($"dotnet build \"{service.Status.ProjectFilePath}\" /nologo");

                var buildResult = await ProcessUtil.RunAsync("dotnet", $"build \"{service.Status.ProjectFilePath}\" /nologo",
                                                            outputDataReceived: data => service.Logs.OnNext(data),
                                                            throwOnError: false);

                if (buildResult.ExitCode != 0)
                {
                    _logger.LogInformation("Building {ProjectFile} failed with exit code {ExitCode}: " + buildResult.StandardOutput + buildResult.StandardError, service.Status.ProjectFilePath, buildResult.ExitCode);
                    return;
                }
            }

            async Task RunApplicationAsync(IEnumerable<(int Port, int BindingPort, string Protocol)> ports)
            {
                var hasPorts = ports.Any();

                var environment = new Dictionary<string, string>
                {
                    // Default to development environment
                    ["DOTNET_ENVIRONMENT"] = "Development"
                };

                application.PopulateEnvironment(service, (k, v) => environment[k] = v);

                if (_debugMode)
                {
                    environment["DOTNET_STARTUP_HOOKS"] = typeof(Hosting.Runtime.HostingRuntimeHelpers).Assembly.Location;
                }

                if (hasPorts)
                {
                    // These ports should also be passed in not assuming ASP.NET Core
                    environment["ASPNETCORE_URLS"] = string.Join(";", ports.Select(p => $"{p.Protocol ?? "http"}://localhost:{p.Port}"));

                    foreach (var p in ports)
                    {
                        environment[$"{p.Protocol?.ToUpper() ?? "HTTP"}_PORT"] = p.BindingPort.ToString();
                    }
                }

                while (!processInfo.StoppedTokenSource.IsCancellationRequested)
                {
                    var replica = serviceName + "_" + Guid.NewGuid().ToString().Substring(0, 10).ToLower();
                    var status = new ProcessStatus(service, replica);
                    service.Replicas[replica] = status;

                    service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Added, status));

                    // This isn't your host name
                    environment["APP_INSTANCE"] = replica;

                    status.ExitCode = null;
                    status.Pid = null;
                    status.Environment = environment;

                    if (hasPorts)
                    {
                        status.Ports = ports.Select(p => p.Port);
                    }

                    _logger.LogInformation("Launching service {ServiceName}: {ExePath} {args}", replica, path, args);

                    try
                    {
                        service.Logs.OnNext($"[{replica}]:{path} {args}");

                        var result = await ProcessUtil.RunAsync(path, args,
                            environmentVariables: environment,
                            workingDirectory: workingDirectory,
                            outputDataReceived: data => service.Logs.OnNext($"[{replica}]: {data}"),
                            onStart: pid =>
                            {
                                if (hasPorts)
                                {
                                    _logger.LogInformation("{ServiceName} running on process id {PID} bound to {Address}", replica, pid, string.Join(", ", ports.Select(p => $"{p.Protocol ?? "http"}://localhost:{p.Port}")));
                                }
                                else
                                {
                                    _logger.LogInformation("{ServiceName} running on process id {PID}", replica, pid);
                                }

                                status.Pid = pid;

                                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Started, status));
                            },
                            throwOnError: false,
                            cancellationToken: processInfo.StoppedTokenSource.Token);

                        status.ExitCode = result.ExitCode;

                        if (status.Pid != null)
                        {
                            service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Stopped, status));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(0, ex, "Failed to launch process for service {ServiceName}", replica);

                        Thread.Sleep(5000);
                    }

                    service.Restarts++;

                    if (status.ExitCode != null)
                    {
                        _logger.LogInformation("{ServiceName} process exited with exit code {ExitCode}", replica, status.ExitCode);
                    }

                    // Remove the replica from the set
                    service.Replicas.TryRemove(replica, out _);
                    service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Removed, status));
                }
            }

            if (serviceDescription.Bindings.Count > 0)
            {
                // Each replica is assigned a list of internal ports, one mapped to each external
                // port
                for (int i = 0; i < serviceDescription.Replicas; i++)
                {
                    var ports = new List<(int, int, string)>();
                    foreach (var binding in serviceDescription.Bindings)
                    {
                        if (binding.Port == null)
                        {
                            continue;
                        }

                        ports.Add((service.PortMap[binding.Port.Value][i], binding.Port.Value, binding.Protocol));
                    }

                    processInfo.Tasks[i] = RunApplicationAsync(ports);
                }
            }
            else
            {
                for (int i = 0; i < service.Description.Replicas; i++)
                {
                    processInfo.Tasks[i] = RunApplicationAsync(Enumerable.Empty<(int, int, string)>());
                }
            }

            service.Items[typeof(ProcessInfo)] = processInfo;
        }

        private Task KillRunningProcesses(IDictionary<string, Service> services)
        {
            static async Task KillProcessAsync(Service service)
            {
                if (service.Items.TryGetValue(typeof(ProcessInfo), out var stateObj) && stateObj is ProcessInfo state)
                {
                    // Cancel the token before stopping the process
                    state.StoppedTokenSource.Cancel();

                    await Task.WhenAll(state.Tasks);
                }
            }

            var index = 0;
            var tasks = new Task[services.Count];
            foreach (var s in services.Values)
            {
                var state = s;
                tasks[index++] = KillProcessAsync(state);
            }

            return Task.WhenAll(tasks);
        }

        private static string GetExePath(string projectFilePath)
        {
            // TODO: Use msbuild to get the target path

            var outputFileName = Path.GetFileNameWithoutExtension(projectFilePath) + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "");

            var debugOutputPath = Path.Combine(Path.GetDirectoryName(projectFilePath), "bin", "Debug");

            var tfms = Directory.Exists(debugOutputPath) ? Directory.GetDirectories(debugOutputPath) : Array.Empty<string>();

            if (tfms.Length > 0)
            {
                // Pick the first one
                var path = Path.Combine(debugOutputPath, tfms[0], outputFileName);
                if (File.Exists(path))
                {
                    return path;
                }

                // Older versions of .NET Core didn't have TFMs
                return Path.Combine(debugOutputPath, tfms[0], Path.GetFileNameWithoutExtension(projectFilePath) + ".dll");
            }

            return Path.Combine(debugOutputPath, "netcoreapp3.1", outputFileName);
        }

        private class ProcessInfo
        {
            public Task[] Tasks { get; set; }

            public CancellationTokenSource StoppedTokenSource { get; set; } = new CancellationTokenSource();
        }
    }
}
