﻿// Licensed to the .NET Foundation under one or more agreements.
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

        public ProcessRunner(ILogger logger, ReplicaRegistry replicaRegistry, ProcessRunnerOptions options)
        {
            _logger = logger;
            _replicaRegistry = replicaRegistry;
            _options = options;
        }

        public async Task StartAsync(Application application)
        {
            await PurgeFromPreviousRun();

            await BuildAndRunProjects(application);
        }

        public Task StopAsync(Application application)
        {
            return KillRunningProcesses(application.Services);
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
                    workingDirectory = project.ProjectFile.Directory.FullName;
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
                sb.AppendLine(@"<Project DefaultTargets=""Build"">
    <ItemGroup>");

                foreach (var group in projectGroups)
                {
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

                var buildResult = await ProcessUtil.RunAsync("dotnet", $"build \"{projectPath}\" /nologo", throwOnError: false, workingDirectory: application.ContextDirectory);

                if (buildResult.ExitCode != 0)
                {
                    _logger.LogInformation("Building projects failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, buildResult.ExitCode);
                    return;
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
                    };
                }
            }
        }

        private void LaunchService(Application application, Service service)
        {
            var serviceDescription = service.Description;
            var processInfo = new ProcessInfo(new Task[service.Description.Replicas]);
            var serviceName = serviceDescription.Name;

            // Set by BuildAndRunService
            var args = service.Status.Args!;
            var path = service.Status.ExecutablePath!;
            var workingDirectory = service.Status.WorkingDirectory!;

            async Task RunApplicationAsync(IEnumerable<(int ExternalPort, int Port, string? Protocol)> ports)
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

                if (_options.DebugMode && (_options.DebugAllServices || _options.ServicesToDebug.Contains(serviceName, StringComparer.OrdinalIgnoreCase)))
                {
                    environment["DOTNET_STARTUP_HOOKS"] = typeof(Hosting.Runtime.HostingRuntimeHelpers).Assembly.Location;
                }

                if (hasPorts)
                {
                    // We need to bind to all interfaces on linux since the container -> host communication won't work
                    // if we use the IP address to reach out of the host. This works fine on osx and windows
                    // but doesn't work on linux.
                    var host = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "*" : "localhost";

                    // These are the ports that the application should use for binding

                    // 1. Configure ASP.NET Core to bind to those same ports
                    environment["ASPNETCORE_URLS"] = string.Join(";", ports.Select(p => $"{p.Protocol ?? "http"}://{host}:{p.Port}"));

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

                    // TODO clean this up.
                    foreach (var env in environment)
                    {
                        args = args.Replace($"%{env.Key}%", env.Value);
                    }

                    _logger.LogInformation("Launching service {ServiceName}: {ExePath} {args}", replica, path, args);

                    try
                    {
                        service.Logs.OnNext($"[{replica}]:{path} {args}");

                        var result = await ProcessUtil.RunAsync(
                            path,
                            args,
                            environmentVariables: environment,
                            workingDirectory: workingDirectory,
                            outputDataReceived: data => service.Logs.OnNext($"[{replica}]: {data}"),
                            errorDataReceived: data => service.Logs.OnNext($"[{replica}]: {data}"),
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

                                WriteReplicaToStore(pid.ToString());
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

                        ports.Add((binding.Port.Value, binding.ReplicaPorts[i], binding.Protocol));
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
            static Task KillProcessAsync(Service service)
            {
                if (service.Items.TryGetValue(typeof(ProcessInfo), out var stateObj) && stateObj is ProcessInfo state)
                {
                    // Cancel the token before stopping the process
                    state.StoppedTokenSource.Cancel();

                    return Task.WhenAll(state.Tasks);
                }
                return Task.CompletedTask;
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
            return process.MainModule.FileName;
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
