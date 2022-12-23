// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class ProcessRunner : IApplicationProcessor
    {
        private const string ProcessReplicaStore = "process";

        private readonly ILogger _logger;
        private readonly ProcessRunnerOptions _options;
        private readonly ReplicaRegistry _replicaRegistry;

        private readonly BuildWatcher _watchBuilderWorker;

        public ProcessRunner(ILogger logger, ReplicaRegistry replicaRegistry, ProcessRunnerOptions options)
        {
            _logger = logger;
            _replicaRegistry = replicaRegistry;
            _options = options;

            _watchBuilderWorker = new BuildWatcher(logger);
        }

        public async Task StartAsync(Application application)
        {
            await PurgeFromPreviousRun();

            await _watchBuilderWorker.StartAsync(application.BuildSolution, application.ContextDirectory);

            await BuildAndRunProjects(application);
        }

        public async Task StopAsync(Application application)
        {
            await _watchBuilderWorker.StopAsync();

            await KillRunningProcesses(application.Services);
        }

        private async Task BuildAndRunProjects(Application application)
        {
            var projectGroups = new Dictionary<string, ProjectGroup>();
            var groupCount = 0;

            foreach (var service in application.Services.Values)
            {
                var serviceDescription = service.Description;

                string path;
                string args;
                var buildProperties = string.Empty;
                string workingDirectory;
                if (serviceDescription.RunInfo is ProjectRunInfo project)
                {
                    path = project.RunCommand;
                    workingDirectory = project.ProjectFile.Directory!.FullName;
                    args = project.Args == null ? project.RunArguments : project.RunArguments + " " + project.Args;
                    buildProperties = project.BuildProperties.Aggregate(string.Empty, (current, property) => current + $";{property.Key}={property.Value}").TrimStart(';');

                    service.Status.ProjectFilePath = project.ProjectFile.FullName;
                }
                else if (serviceDescription.RunInfo is ExecutableRunInfo executable)
                {
                    path = executable.Executable;
                    workingDirectory = executable.WorkingDirectory!;
                    args = executable.Args ?? "";
                }
                else if (serviceDescription.RunInfo is AzureFunctionRunInfo function)
                {
                    path = function.FuncExecutablePath!;
                    workingDirectory = new DirectoryInfo(function.FunctionPath).FullName;
                    // todo make sure to exclude functions app from implied tye running.

                    args = function.Args ?? $"start --build";
                }
                else
                {
                    continue;
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

                // TODO instead of always building with projects, try building with sln if available.
                if (service.Status.ProjectFilePath != null &&
                    service.Description.RunInfo is ProjectRunInfo project2 &&
                    project2.Build &&
                    _options.BuildProjects)
                {
                    if (!projectGroups.TryGetValue(buildProperties, out var projectGroup))
                    {
                        projectGroup = new ProjectGroup("ProjectGroup" + groupCount);
                        projectGroups[buildProperties] = projectGroup;
                        groupCount++;
                    }

                    projectGroup.Services.Add(service);
                }
            }

            if (projectGroups.Count > 0)
            {
                using var directory = TempDirectory.Create();

                var projectPath = Path.Combine(directory.DirectoryPath, Path.GetRandomFileName() + ".proj");

                var sb = new StringBuilder();
                sb.AppendLine(@"<Project DefaultTargets=""Build"">");

                foreach (var group in projectGroups)
                {
                    sb.AppendLine(@"    <ItemGroup>");
                    foreach (var p in group.Value.Services)
                    {
                        sb.AppendLine($"        <{group.Value.GroupName} Include=\"{p.Status.ProjectFilePath}\" />");
                    }
                    sb.AppendLine(@"    </ItemGroup>");
                }

                sb.AppendLine($@"    <Target Name=""Build"">");
                foreach (var group in projectGroups)
                {
                    sb.AppendLine($@"        <MsBuild Projects=""@({group.Value.GroupName})"" Properties=""{group.Key}"" BuildInParallel=""true"" />");
                }

                sb.AppendLine("    </Target>");
                sb.AppendLine("</Project>");
                File.WriteAllText(projectPath, sb.ToString());

                _logger.LogInformation("Building projects");

                var buildResult = await ProcessUtil.RunAsync("dotnet", $"build --no-restore \"{projectPath}\" /nologo", throwOnError: false, workingDirectory: application.ContextDirectory);

                if (buildResult.ExitCode != 0)
                {
                    throw new TyeBuildException($"Building projects failed with exit code {buildResult.ExitCode}: \r\n{buildResult.StandardOutput}");
                }
            }

            foreach (var s in application.Services)
            {
                switch (s.Value.ServiceType)
                {
                    case ServiceType.Executable:
                        LaunchService(application, s.Value);
                        break;
                    case ServiceType.Project:
                        LaunchService(application, s.Value);
                        break;
                    case ServiceType.Function:
                        LaunchService(application, s.Value);
                        break;
                };
            }
        }

        private void LaunchService(Application application, Service service)

        {
            var processInfo = (service.Items.ContainsKey(typeof(ProcessInfo)) ? (ProcessInfo?)service.Items[typeof(ProcessInfo)] : null)
                                      ?? new ProcessInfo(new Task[service.Description.Replicas]);
            var serviceName = service.Description.Name;

            // Set by BuildAndRunService
            var args = service.Status.Args!;
            var path = service.Status.ExecutablePath!;
            var workingDirectory = service.Status.WorkingDirectory!;

            async Task RunApplicationAsync(IEnumerable<(int ExternalPort, int Port, string? Protocol, string? Host)> ports, string copiedArgs)
            {
                // Make sure we yield before trying to start the process, this is important so we don't block startup
                await Task.Yield();

                var hasPorts = ports.Any();

                var environment = new Dictionary<string, string>
                {
                    // Default to development environment
                    ["DOTNET_ENVIRONMENT"] = "Development",
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    // Remove the color codes from the console output
                    ["DOTNET_LOGGING__CONSOLE__DISABLECOLORS"] = "true",
                    ["ASPNETCORE_LOGGING__CONSOLE__DISABLECOLORS"] = "true"
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

                if (_options.DebugMode && (_options.DebugAllServices || _options.ServicesToDebug!.Contains(serviceName, StringComparer.OrdinalIgnoreCase)))
                {
                    environment["DOTNET_STARTUP_HOOKS"] = typeof(Hosting.Runtime.HostingRuntimeHelpers).Assembly.Location;
                }

                if (hasPorts)
                {
                    // These are the ports that the application should use for binding

                    // 1. Configure ASP.NET Core to bind to those same ports
                    var urlPorts = ports.Where(p => p.Protocol == null || p.Protocol == "http" || p.Protocol == "https");
                    environment["ASPNETCORE_URLS"] = string.Join(";", urlPorts.Select(p => $"{p.Protocol ?? "http"}://{p.Host ?? application.ContainerEngine.AspNetUrlsHost}:{p.Port}"));

                    // Set the HTTPS port for the redirect middleware
                    foreach (var p in ports)
                    {
                        if (string.Equals(p.Protocol, "https", StringComparison.OrdinalIgnoreCase))
                        {
                            // We need to set the redirect URL to the exposed port so the redirect works cleanly
                            environment["HTTPS_PORT"] = p.ExternalPort.ToString();
                        }
                    }

                    // 3. For non-ASP.NET Core apps, pass the same information in the PORT env variable as a semicolon separated list.
                    environment["PORT"] = string.Join(";", ports.Select(p => $"{p.Port}"));

                    if (service.ServiceType == ServiceType.Function)
                    {
                        // Need to inject port and UseHttps as an argument to func.exe rather than environment variables.
                        var binding = ports.First();
                        copiedArgs += $" --port {binding.Port}";
                        if (binding.Protocol == "https")
                        {
                            copiedArgs += " --useHttps";
                        }
                    }
                }

                var backOff = TimeSpan.FromSeconds(5);

                while (!processInfo!.StoppedTokenSource.IsCancellationRequested)
                {
                    var replica = serviceName + "_" + Guid.NewGuid().ToString().Substring(0, 10).ToLower();
                    var status = new ProcessStatus(service, replica);

                    using var stoppingCts = new CancellationTokenSource();
                    status.StoppingTokenSource = stoppingCts;
                    await using var _ = processInfo.StoppedTokenSource.Token.Register(() => status.StoppingTokenSource.Cancel());

                    if (!_options.Watch)
                    {
                        service.Replicas[replica] = status;
                        service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Added, status));
                    }

                    // This isn't your host name
                    environment["APP_INSTANCE"] = replica;

                    status.ExitCode = null;
                    status.Pid = null;
                    status.Environment = environment;

                    if (hasPorts)
                    {
                        status.Ports = ports.Select(p => p.Port);
                        status.Bindings = ports.Select(p => new ReplicaBinding() { Port = p.Port, ExternalPort = p.ExternalPort, Protocol = p.Protocol }).ToList();
                    }

                    // TODO clean this up.
                    foreach (var env in environment)
                    {
                        copiedArgs = copiedArgs.Replace($"%{env.Key}%", env.Value);
                    }

                    _logger.LogInformation("Launching service {ServiceName}: {ExePath} {args}", replica, path, copiedArgs);

                    try
                    {
                        service.Logs.OnNext($"[{replica}]:{path} {copiedArgs}");
                        var processSpec = new ProcessSpec
                        {
                            Executable = path,
                            WorkingDirectory = workingDirectory,
                            Arguments = copiedArgs,
                            EnvironmentVariables = environment,
                            OutputData = data =>
                            {
                                service.Logs.OnNext($"[{replica}]: {data}");
                            },
                            ErrorData = data => service.Logs.OnNext($"[{replica}]: {data}"),
                            OnStart = pid =>
                            {
                                if (hasPorts)
                                {
                                    _logger.LogInformation("{ServiceName} running on process id {PID} bound to {Address}",
                                        replica, pid, string.Join(", ", ports.Select(p => $"{p.Protocol ?? "http"}://{p.Host ?? application.ContainerEngine.AspNetUrlsHost}:{p.Port}")));
                                }
                                else
                                {
                                    _logger.LogInformation("{ServiceName} running on process id {PID}", replica, pid);
                                }

                                // Reset the backoff
                                backOff = TimeSpan.FromSeconds(5);

                                status.Pid = pid;

                                WriteReplicaToStore(pid.ToString());

                                if (_options.Watch)
                                {
                                    // OnStart/OnStop will be called multiple times for watch.
                                    // Watch will constantly be adding and removing from the list, so only add here for watch.
                                    service.Replicas[replica] = status;
                                    service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Added, status));
                                }

                                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Started, status));
                            },
                            OnStop = exitCode =>
                            {
                                status.ExitCode = exitCode;

                                if (status.Pid != null)
                                {
                                    service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Stopped, status));
                                }

                                if (!_options.Watch)
                                {
                                    // Only increase backoff when not watching project as watch will wait for file changes before rebuild.
                                    backOff *= 2;
                                }
                                if (!processInfo.StoppedTokenSource.IsCancellationRequested)
                                {
                                    service.Restarts++;
                                }

                                service.Replicas.TryRemove(replica, out var _);
                                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Removed, status));

                                if (status.ExitCode != null)
                                {
                                    _logger.LogInformation("{ServiceName} process exited with exit code {ExitCode}", replica, status.ExitCode);
                                }
                            },
                            Build = async () =>
                            {
                                if (service.Description.RunInfo is ProjectRunInfo)
                                {
                                    var exitCode = await _watchBuilderWorker!.BuildProjectFileAsync(service.Status.ProjectFilePath!);
                                    _logger.LogInformation($"Built {service.Status.ProjectFilePath} with exit code {exitCode}");
                                    return exitCode;
                                }

                                return 0;
                            }
                        };

                        if (_options.Watch && (service.Description.RunInfo is ProjectRunInfo runInfo))
                        {
                            var projectFile = runInfo.ProjectFile.FullName;
                            var fileSetFactory = new MsBuildFileSetFactory(_logger,
                                projectFile,
                                waitOnError: true,
                                trace: false);
                            environment["DOTNET_WATCH"] = "1";

                            await new DotNetWatcher(_logger)
                                .WatchAsync(processSpec, fileSetFactory, replica, status.StoppingTokenSource.Token);
                        }
                        else if (_options.Watch && (service.Description.RunInfo is AzureFunctionRunInfo azureFunctionRunInfo) && !string.IsNullOrEmpty(azureFunctionRunInfo.ProjectFile))
                        {
                            var projectFile = azureFunctionRunInfo.ProjectFile;
                            var fileSetFactory = new MsBuildFileSetFactory(_logger,
                                projectFile,
                                waitOnError: true,
                                trace: false);
                            environment["DOTNET_WATCH"] = "1";

                            await new DotNetWatcher(_logger)
                                .WatchAsync(processSpec, fileSetFactory, replica, status.StoppingTokenSource.Token);
                        }
                        else
                        {
                            await ProcessUtil.RunAsync(processSpec, status.StoppingTokenSource.Token, throwOnError: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(0, ex, "Failed to launch process for service {ServiceName}", replica);

                        if (!_options.Watch)
                        {
                            // Only increase backoff when not watching project as watch will wait for file changes before rebuild.
                            backOff *= 2;
                        }

                        service.Restarts++;
                        service.Replicas.TryRemove(replica, out var _);

                        try
                        {
                            await Task.Delay(backOff, processInfo.StoppedTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // Swallow cancellation exceptions and continue
                        }
                    }
                }
            }

            void Start()
            {
                if (service.Description!.Bindings.Count > 0)
                {
                    // Each replica is assigned a list of internal ports, one mapped to each external
                    // port
                    for (int i = 0; i < service.Description.Replicas; i++)
                    {
                        var ports = new List<(int, int, string?, string?)>();
                        foreach (var binding in service.Description.Bindings)
                        {
                            if (binding.Port == null)
                            {
                                continue;
                            }

                            ports.Add((binding.Port.Value, binding.ReplicaPorts[i], binding.Protocol, binding.Host));
                        }

                        processInfo!.Tasks[i] = RunApplicationAsync(ports, args);
                    }
                }
                else
                {
                    for (int i = 0; i < service.Description.Replicas; i++)
                    {
                        processInfo!.Tasks[i] = RunApplicationAsync(Enumerable.Empty<(int, int, string?, string?)>(), args);
                    }
                }
            }

            processInfo.Start = Start;
            service.Items[typeof(ProcessInfo)] = processInfo;
            if (!_options.ManualStartServices && !(_options.ServicesNotToStart?.Contains(serviceName, StringComparer.OrdinalIgnoreCase) ?? false))
            {
                processInfo.Start();
            }
            else
            {
                for (int i = 0; i < processInfo.Tasks.Length; i++)
                {
                    processInfo.Tasks[i] = Task.CompletedTask;
                }
            }
        }

        public static async Task RestartService(Service service)
        {
            if (service.Items.TryGetValue(typeof(ProcessInfo), out var stateObj) && stateObj is ProcessInfo state)
            {
                await KillProcessAsync(service);
                service.Restarts++;
                state.Start?.Invoke();
                await Task.WhenAll(state.Tasks);
            }
        }

        public static async Task KillProcessAsync(Service service)
        {
            if (service.Items.TryGetValue(typeof(ProcessInfo), out var stateObj) && stateObj is ProcessInfo state)
            {
                // Cancel the token before stopping the process
                state.StoppedTokenSource?.Cancel();

                await Task.WhenAll(state.Tasks);
                state.ResetStoppedTokenSource();
            }
        }

        private Task KillRunningProcesses(IDictionary<string, Service> services)
        {

            var index = 0;
            var tasks = new Task[services.Count];
            foreach (var s in services.Values)
            {
                var state = s;
                tasks[index++] = KillProcessAsync(state);
            }

            return Task.WhenAll(tasks);
        }

        private async Task PurgeFromPreviousRun()
        {
            var processReplicas = await _replicaRegistry.GetEvents(ProcessReplicaStore);
            foreach (var replica in processReplicas)
            {
                if (int.TryParse(replica["pid"], out var pid))
                {
                    ProcessUtil.KillProcess(pid);
                    _logger.LogInformation("removed process {pid} from previous run", pid);
                }
            }

            _replicaRegistry.DeleteStore(ProcessReplicaStore);
        }

        private void WriteReplicaToStore(string pid)
        {
            _replicaRegistry.WriteReplicaEvent(ProcessReplicaStore, new Dictionary<string, string>()
            {
                ["pid"] = pid
            });
        }

        private static string? GetDotnetRoot()
        {
            var entryPointFilePath = GetEntryPointFilePath();

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

        private static string GetEntryPointFilePath()
        {
            using var process = Process.GetCurrentProcess();
            return process.MainModule!.FileName!;
        }

        private class ProcessInfo
        {

            public ProcessInfo(Task[] tasks)
            {
                Tasks = tasks;
            }

            public Task[] Tasks { get; }

            public CancellationTokenSource StoppedTokenSource { get; private set; } = new CancellationTokenSource();
            public Action? Start { get; internal set; }
            internal void ResetStoppedTokenSource()
            {
                StoppedTokenSource.Dispose();
                StoppedTokenSource = new CancellationTokenSource();
            }
        }

        private class ProjectGroup
        {
            public ProjectGroup(string groupName)
            {
                GroupName = groupName;
            }
            public List<Service> Services { get; } = new List<Service>();
            public string GroupName { get; }
        }
    }
}
