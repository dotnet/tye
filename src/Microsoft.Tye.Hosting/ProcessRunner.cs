// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class ProcessRunner : IApplicationProcessor
    {
        private readonly ILogger _logger;
        private readonly ProcessRunnerOptions _options;

        public ProcessRunner(ILogger logger, ProcessRunnerOptions options)
        {
            _logger = logger;
            _options = options;
        }

        public Task StartAsync(Application application)
        {
            var tasks = new Task[application.Services.Count];
            var index = 0;
            foreach (var s in application.Services)
            {
                tasks[index++] = s.Value.ServiceType switch
                {
                    ServiceType.Executable => LaunchService(application, s.Value),
                    ServiceType.Project => LaunchService(application, s.Value),

                    _ => Task.CompletedTask,
                };
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
            var serviceName = serviceDescription.Name;

            var path = "";
            var workingDirectory = "";
            var args = "";

            if (serviceDescription.RunInfo is ProjectRunInfo project)
            {
                path = project.RunCommand;
                workingDirectory = project.ProjectFile.Directory.FullName;
                args = project.Args == null ? project.RunArguments : project.RunArguments + " " + project.Args;
                service.Status.ProjectFilePath = project.ProjectFile.FullName;
            }
            else if (serviceDescription.RunInfo is ExecutableRunInfo executable)
            {
                path = executable.Executable;
                workingDirectory = executable.WorkingDirectory!;
                args = executable.Args ?? "";
            }
            else
            {
                throw new InvalidOperationException("Unsupported ServiceType.");
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

            var processInfo = new ProcessInfo(new Task[service.Description.Replicas]);

            if (service.Status.ProjectFilePath != null &&
                service.Description.RunInfo is ProjectRunInfo project2 &&
                project2.Build &&
                _options.BuildProjects)
            {
                // Sometimes building can fail because of file locking (like files being open in VS)
                _logger.LogInformation("Building project {ProjectFile}", service.Status.ProjectFilePath);

                service.Logs.OnNext($"dotnet build \"{service.Status.ProjectFilePath}\" /nologo");

                var buildResult = await ProcessUtil.RunAsync("dotnet", $"build \"{service.Status.ProjectFilePath}\" /nologo", throwOnError: false);

                service.Logs.OnNext(buildResult.StandardOutput);

                if (buildResult.ExitCode != 0)
                {
                    _logger.LogInformation("Building {ProjectFile} failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, service.Status.ProjectFilePath, buildResult.ExitCode);
                    return;
                }
            }

            async Task RunApplicationAsync(IEnumerable<(int Port, int BindingPort, string? Protocol)> ports)
            {
                // Make sure we yield before trying to start the process, this is important so we don't hang startup
                await Task.Yield();

                var hasPorts = ports.Any();

                var environment = new Dictionary<string, string>
                {
                    // Default to development environment
                    ["DOTNET_ENVIRONMENT"] = "Development"
                };

                // Set up environment variables to use the version of dotnet we're using to run
                // this is important for tests where we're not using a globally-installed dotnet.
                var dotnetRoot = GetDotnetRoot();
                if (dotnetRoot is object)
                {
                    environment["DOTNET_ROOT"] = dotnetRoot;
                    environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
                    environment["PATH"] = $"{dotnetRoot};{Environment.GetEnvironmentVariable("PATH")}";
                }

                application.PopulateEnvironment(service, (k, v) => environment[k] = v);

                if (_options.DebugMode && (_options.DebugAllServices || _options.ServicesToDebug.Contains(serviceName, StringComparer.OrdinalIgnoreCase)))
                {
                    environment["DOTNET_STARTUP_HOOKS"] = typeof(Hosting.Runtime.HostingRuntimeHelpers).Assembly.Location;
                }

                if (hasPorts)
                {
                    // These are the ports that the application should use for binding

                    // 1. Configure ASP.NET Core to bind to those same ports
                    environment["ASPNETCORE_URLS"] = string.Join(";", ports.Select(p => $"{p.Protocol ?? "http"}://localhost:{p.Port}"));

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
                    environment["PORT"] = string.Join(";", ports.Select(p => $"{p.Port}"));
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

                        try
                        {
                            await Task.Delay(5000, processInfo.StoppedTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // Swallow cancellation exceptions and continue
                        }
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
                    var ports = new List<(int, int, string?)>();
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
                    processInfo.Tasks[i] = RunApplicationAsync(Enumerable.Empty<(int, int, string?)>());
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

        private static string? GetDotnetRoot()
        {
            var process = Process.GetCurrentProcess();
            var entryPointFilePath = process.MainModule.FileName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                Path.GetFileNameWithoutExtension(entryPointFilePath) == "dotnet")
            {
                return Path.GetDirectoryName(entryPointFilePath);
            }
            else if (Path.GetFileName(entryPointFilePath) == "dotnet")
            {
                return Path.GetDirectoryName(entryPointFilePath);
            }

            return null;
        }

        private class ProcessInfo
        {

            public ProcessInfo(Task[] tasks)
            {
                Tasks = tasks;
            }

            public Task[] Tasks { get; }

            public CancellationTokenSource StoppedTokenSource { get; } = new CancellationTokenSource();
        }
    }
}
