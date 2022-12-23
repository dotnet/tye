// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Tye.ConfigModel;

namespace Microsoft.Tye
{
    public static class ApplicationFactory
    {
        public static async Task<ApplicationBuilder> CreateAsync(OutputContext output, FileInfo source, string? framework = null, ApplicationFactoryFilter? filter = null)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var queue = new Queue<(ConfigApplication, HashSet<string>)>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootConfig = ConfigFactory.FromFile(source);
            rootConfig.Validate();

            var root = new ApplicationBuilder(source, rootConfig.Name!, new ContainerEngine(rootConfig.ContainerEngineType), rootConfig.DashboardPort)
            {
                Namespace = rootConfig.Namespace,
                BuildSolution = rootConfig.BuildSolution,
            };

            queue.Enqueue((rootConfig, new HashSet<string>()));

            while (queue.TryDequeue(out var item))
            {
                // dependencies represents a set of all dependencies
                var (config, dependencies) = item;

                if (!visited.Add(config.Source.FullName))
                {
                    continue;
                }

                if (config == rootConfig && config.Registry != null)
                {
                    root.Registry = new ContainerRegistry(config.Registry.Hostname, config.Registry.PullSecret);
                }

                if (config == rootConfig)
                {
                    root.Network = rootConfig.Network;
                }

                foreach (var configExtension in config.Extensions)
                {
                    var extension = new ExtensionConfiguration((string)configExtension["name"]);
                    foreach (var kvp in configExtension)
                    {
                        if (kvp.Key == "name")
                        {
                            continue;
                        }

                        extension.Data.Add(kvp.Key, kvp.Value);
                    }

                    root.Extensions.Add(extension);
                }

                bool IsAzureFunctionService(ConfigService service)
                {
                    return !string.IsNullOrEmpty(service.AzureFunction);
                }

                var services = filter?.ServicesFilter != null ?
                    config.Services.Where(filter.ServicesFilter).ToList() :
                    config.Services;

                // Infer project file for Azure Function services so they will be evaluated.
                foreach (var service in services.Where(IsAzureFunctionService))
                {
                    var azureFunctionDirectory = Path.Combine(config.Source.DirectoryName!, service.AzureFunction!);

                    foreach (var proj in Directory.EnumerateFiles(azureFunctionDirectory))
                    {
                        var fileInfo = new FileInfo(proj);
                        if (fileInfo.Extension == ".csproj" || fileInfo.Extension == ".fsproj")
                        {
                            service.Project = fileInfo.FullName;
                            break;
                        }
                    }
                }

                var sw = Stopwatch.StartNew();
                // Project services will be restored and evaluated before resolving all other services.
                // This batching will mitigate the performance cost of running MSBuild out of process.
                var projectServices = services.Where(s => !string.IsNullOrEmpty(s.Project));
                var projectMetadata = new Dictionary<string, string>();

                var msbuildEvaluationResult = await EvaluateProjectsAsync(
                    projects: projectServices,
                    configRoot: config.Source.DirectoryName!,
                    output: output);
                var msbuildEvaluationOutput = msbuildEvaluationResult
                    .StandardOutput
                    .Split(Environment.NewLine);

                var multiTFMProjects = new List<ConfigService>();

                foreach (var line in msbuildEvaluationOutput)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Microsoft.Tye metadata: "))
                    {
                        var values = line.Split(':', 3);
                        var projectName = values[1].Trim();
                        var metadataPath = values[2].Trim();
                        projectMetadata.Add(projectName, metadataPath);

                        output.WriteDebugLine($"Resolved metadata for service {projectName} at {metadataPath}");
                    }
                    else if (trimmed.StartsWith("Microsoft.Tye cross-targeting project: "))
                    {
                        var values = line.Split(':', 2);
                        var projectName = values[1].Trim();

                        var multiTFMConfigService = projectServices.First(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
                        multiTFMConfigService.BuildProperties.Add(new BuildProperty { Name = "TargetFramework", Value = framework ?? string.Empty });
                        multiTFMProjects.Add(multiTFMConfigService);
                    }
                }

                if (multiTFMProjects.Any())
                {
                    output.WriteDebugLine("Re-evaluating multi-targeted projects");

                    var multiTFMEvaluationResult = await EvaluateProjectsAsync(
                        projects: multiTFMProjects,
                        configRoot: config.Source.DirectoryName!,
                        output: output);
                    var multiTFMEvaluationOutput = multiTFMEvaluationResult
                        .StandardOutput
                        .Split(Environment.NewLine);

                    foreach (var line in multiTFMEvaluationOutput)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("Microsoft.Tye metadata: "))
                        {
                            var values = line.Split(':', 3);
                            var projectName = values[1].Trim();
                            var metadataPath = values[2].Trim();
                            projectMetadata.Add(projectName, metadataPath);

                            output.WriteDebugLine($"Resolved metadata for service {projectName} at {metadataPath}");
                        }
                        else if (trimmed.StartsWith("Microsoft.Tye cross-targeting project: "))
                        {
                            var values = line.Split(':', 2);
                            var projectName = values[1].Trim();
                            throw new CommandException($"Unable to run {projectName}. Your project targets multiple frameworks. Specify which framework to run using '--framework' or a build property in tye.yaml.");
                        }
                    }
                }

                output.WriteDebugLine($"Restore and project evaluation took: {sw.Elapsed.TotalMilliseconds}ms");

                foreach (var configService in services)
                {
                    ServiceBuilder service;
                    if (root.Services.Any(s => s.Name == configService.Name))
                    {
                        // Even though this service has already created a service, we still need
                        // to update dependency information
                        AddToRootServices(root, dependencies, configService.Name);
                        continue;
                    }

                    // NOTE: Evaluate Azure Function services before project services as both use Project.
                    if (IsAzureFunctionService(configService))
                    {
                        var azureFunctionDirectory = Path.Combine(config.Source.DirectoryName!, configService.AzureFunction!);

                        var functionBuilder = new AzureFunctionServiceBuilder(
                            configService.Name,
                            azureFunctionDirectory,
                            ServiceSource.Configuration)
                        {
                            Args = configService.Args,
                            Replicas = configService.Replicas ?? 1,
                            FuncExecutablePath = configService.FuncExecutable,
                            ProjectFile = configService.Project
                        };

                        if (functionBuilder.ProjectFile != null)
                        {
                            ProjectReader.ReadAzureFunctionProjectDetails(output, functionBuilder, projectMetadata[configService.Name]);
                        }

                        // TODO liveness?
                        service = functionBuilder;
                    }
                    else if (!string.IsNullOrEmpty(configService.Project))
                    {
                        // TODO: Investigate possible null.
                        var project = new DotnetProjectServiceBuilder(configService.Name!, new FileInfo(configService.ProjectFullPath!), ServiceSource.Configuration);
                        service = project;

                        project.Build = configService.Build ?? true;
                        project.Args = configService.Args;
                        foreach (var buildProperty in configService.BuildProperties)
                        {
                            project.BuildProperties.Add(buildProperty.Name, buildProperty.Value);
                        }
                        project.HotReload = configService.HotReload ?? false;
                        project.Replicas = configService.Replicas ?? 1;
                        project.Liveness = configService.Liveness != null ? GetProbeBuilder(configService.Liveness) : null;
                        project.Readiness = configService.Readiness != null ? GetProbeBuilder(configService.Readiness) : null;

                        // We don't apply more container defaults here because we might need
                        // to prompt for the registry name.
                        project.ContainerInfo = new ContainerInfo() { UseMultiphaseDockerfile = false, };

                        // If project evaluation is successful this should not happen, therefore an exception will be thrown.
                        if (!projectMetadata.ContainsKey(configService.Name))
                        {
                            throw new CommandException($"Evaluated project metadata file could not be found for service {configService.Name}");
                        }

                        ProjectReader.ReadProjectDetails(output, project, projectMetadata[configService.Name]);

                        // Do k8s by default.
                        project.ManifestInfo = new KubernetesManifestInfo();
                    }
                    else if (!string.IsNullOrEmpty(configService.Image))
                    {
                        var container = new ContainerServiceBuilder(configService.Name!, configService.Image!, ServiceSource.Configuration)
                        {
                            Args = configService.Args,
                            Replicas = configService.Replicas ?? 1
                        };
                        service = container;

                        container.Liveness = configService.Liveness != null ? GetProbeBuilder(configService.Liveness) : null;
                        container.Readiness = configService.Readiness != null ? GetProbeBuilder(configService.Readiness) : null;
                    }
                    else if (!string.IsNullOrEmpty(configService.DockerFile))
                    {
                        var dockerFile = new DockerFileServiceBuilder(configService.Name!, configService.Image!, ServiceSource.Configuration)
                        {
                            Args = configService.Args,
                            Build = configService.Build ?? true,
                            Replicas = configService.Replicas ?? 1,
                            DockerFile = Path.Combine(source.DirectoryName!, configService.DockerFile),
                            // Supplying an absolute path with trailing slashes fails for DockerFileContext when calling docker build, so trim trailing slash.
                            DockerFileContext = GetDockerFileContext(source, configService),
                            BuildArgs = configService.DockerFileArgs
                        };
                        service = dockerFile;

                        dockerFile.Liveness = configService.Liveness != null ? GetProbeBuilder(configService.Liveness) : null;
                        dockerFile.Readiness = configService.Readiness != null ? GetProbeBuilder(configService.Readiness) : null;

                        // We don't apply more container defaults here because we might need
                        // to prompt for the registry name.
                        dockerFile.ContainerInfo = new ContainerInfo() { UseMultiphaseDockerfile = false, };

                        // Do k8s by default.
                        dockerFile.ManifestInfo = new KubernetesManifestInfo();
                    }
                    else if (!string.IsNullOrEmpty(configService.Executable))
                    {
                        var expandedExecutable = Environment.ExpandEnvironmentVariables(configService.Executable);
                        var workingDirectory = "";

                        // Special handling of .dlls as executables (it will be executed as dotnet {dll})
                        if (Path.GetExtension(expandedExecutable) == ".dll")
                        {
                            expandedExecutable = Path.GetFullPath(Path.Combine(config.Source.Directory!.FullName, expandedExecutable));
                            workingDirectory = Path.GetDirectoryName(expandedExecutable)!;
                        }

                        var executable = new ExecutableServiceBuilder(configService.Name!, expandedExecutable, ServiceSource.Configuration)
                        {
                            Args = configService.Args,
                            WorkingDirectory = configService.WorkingDirectory != null ?
                            Path.GetFullPath(Path.Combine(config.Source.Directory!.FullName, Environment.ExpandEnvironmentVariables(configService.WorkingDirectory))) :
                            workingDirectory,
                            Replicas = configService.Replicas ?? 1
                        };
                        service = executable;

                        executable.Liveness = configService.Liveness != null ? GetProbeBuilder(configService.Liveness) : null;
                        executable.Readiness = configService.Readiness != null ? GetProbeBuilder(configService.Readiness) : null;
                    }
                    else if (!string.IsNullOrEmpty(configService.Include))
                    {
                        var expandedYaml = Environment.ExpandEnvironmentVariables(configService.Include);

                        var nestedConfig = GetNestedConfig(rootConfig, Path.Combine(config.Source.DirectoryName!, expandedYaml));
                        queue.Enqueue((nestedConfig, new HashSet<string>()));

                        AddToRootServices(root, dependencies, configService.Name);
                        continue;
                    }
                    else if (!string.IsNullOrEmpty(configService.Repository))
                    {
                        // clone to .tye folder
                        var path = configService.CloneDirectory ?? Path.Join(".tye", "deps");
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        var clonePath = Path.Combine(rootConfig.Source.DirectoryName!, path, configService.Name);

                        if (!Directory.Exists(clonePath))
                        {
                            if (!await GitDetector.Instance.IsGitInstalled.Value)
                            {
                                throw new CommandException($"Cannot clone repository {configService.Repository} because git is not installed. Please install git if you'd like to use \"repository\" in tye.yaml.");
                            }

                            var result = await ProcessUtil.RunAsync("git", $"clone {configService.Repository} \"{clonePath}\"", workingDirectory: rootConfig.Source.DirectoryName, throwOnError: false);

                            if (result.ExitCode != 0)
                            {
                                throw new CommandException($"Failed to clone repository {configService.Repository} with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}{result.StandardOutput}.");
                            }
                        }

                        if (!ConfigFileFinder.TryFindSupportedFile(clonePath, out var file, out var errorMessage))
                        {
                            throw new CommandException(errorMessage!);
                        }

                        // pick different service type based on what is in the repo.
                        var nestedConfig = GetNestedConfig(rootConfig, file);

                        queue.Enqueue((nestedConfig, new HashSet<string>()));

                        AddToRootServices(root, dependencies, configService.Name);

                        continue;
                    }
                    else if (configService.External)
                    {
                        var external = new ExternalServiceBuilder(configService.Name, ServiceSource.Configuration);
                        service = external;
                    }
                    else
                    {
                        throw new CommandException("Unable to determine service type.");
                    }

                    // Add dependencies to ourself before adding ourself to avoid self reference
                    service.Dependencies.UnionWith(dependencies);

                    AddToRootServices(root, dependencies, service.Name);

                    root.Services.Add(service);

                    // If there are no bindings and we're in ASP.NET Core project then add an HTTP and HTTPS binding
                    if (configService.Bindings.Count == 0 &&
                        service is ProjectServiceBuilder project2 &&
                        project2.IsAspNet)
                    {
                        // HTTP is the default binding
                        service.Bindings.Add(new BindingBuilder() { Protocol = "http" });
                        service.Bindings.Add(new BindingBuilder() { Name = "https", Protocol = "https" });
                    }
                    else if (configService.Bindings.Count == 0 &&
                        service is AzureFunctionServiceBuilder project3)
                    {
                        // TODO need to figure out binding from host.json file. Supporting http for now.
                        service.Bindings.Add(new BindingBuilder() { Protocol = "http" });
                    }
                    else
                    {
                        foreach (var configBinding in configService.Bindings)
                        {
                            var binding = new BindingBuilder()
                            {
                                Name = configBinding.Name,
                                ConnectionString = configBinding.ConnectionString,
                                Host = configBinding.Host,
                                ContainerPort = configBinding.ContainerPort,
                                Port = configBinding.Port,
                                Protocol = configBinding.Protocol,
                                Routes = configBinding.Routes
                            };

                            // Assume HTTP for projects only (containers may be different)
                            if (binding.ConnectionString == null && configService.Project != null)
                            {
                                binding.Protocol ??= "http";
                            }

                            service.Bindings.Add(binding);
                        }
                    }

                    foreach (var configEnvVar in configService.Configuration)
                    {
                        var envVar = new EnvironmentVariableBuilder(configEnvVar.Name) { Value = configEnvVar.Value, };
                        if (service is ProjectServiceBuilder project)
                        {
                            project.EnvironmentVariables.Add(envVar);
                        }
                        else if (service is ContainerServiceBuilder container)
                        {
                            container.EnvironmentVariables.Add(envVar);
                        }
                        else if (service is ExecutableServiceBuilder executable)
                        {
                            executable.EnvironmentVariables.Add(envVar);
                        }
                        else if (service is AzureFunctionServiceBuilder azureFunction)
                        {
                            azureFunction.EnvironmentVariables.Add(envVar);
                        }
                        else if (service is ExternalServiceBuilder)
                        {
                            throw new CommandException("External services do not support environment variables.");
                        }
                        else
                        {
                            throw new CommandException("Unable to determine service type.");
                        }
                    }

                    foreach (var configVolume in configService.Volumes)
                    {
                        var volume = new VolumeBuilder(configVolume.Source, configVolume.Name, configVolume.Target);
                        if (service is ProjectServiceBuilder project)
                        {
                            project.Volumes.Add(volume);
                        }
                        else if (service is ContainerServiceBuilder container)
                        {
                            container.Volumes.Add(volume);
                        }
                        else if (service is ExecutableServiceBuilder executable)
                        {
                            throw new CommandException("Executable services do not support volumes.");
                        }
                        else if (service is ExternalServiceBuilder)
                        {
                            throw new CommandException("External services do not support volumes.");
                        }
                        else
                        {
                            throw new CommandException("Unable to determine service type.");
                        }
                    }
                }

                var ingresses = filter?.IngressFilter != null ?
                    config.Ingress.Where(filter.IngressFilter).ToList() :
                    config.Ingress;

                foreach (var configIngress in ingresses)
                {
                    var ingress = new IngressBuilder(configIngress.Name!);
                    ingress.Replicas = configIngress.Replicas ?? 1;

                    root.Ingress.Add(ingress);

                    foreach (var configBinding in configIngress.Bindings)
                    {
                        var binding = new IngressBindingBuilder()
                        {
                            Name = configBinding.Name,
                            Port = configBinding.Port,
                            Protocol = configBinding.Protocol ?? "http",
                            IPAddress = configBinding.IPAddress,
                        };
                        ingress.Bindings.Add(binding);
                    }

                    foreach (var configRule in configIngress.Rules)
                    {
                        var rule = new IngressRuleBuilder()
                        {
                            Host = configRule.Host,
                            Path = configRule.Path,
                            PreservePath = configRule.PreservePath,
                            Service = configRule.Service!, // validated elsewhere
                        };
                        ingress.Rules.Add(rule);
                    }
                }
            }

            return root;
        }

        private static async Task<ProcessResult> EvaluateProjectsAsync(IEnumerable<ConfigService> projects, string configRoot, OutputContext output)
        {
            using var directory = TempDirectory.Create();
            var projectPath = Path.Combine(directory.DirectoryPath, Path.GetRandomFileName() + ".proj");

            var sb = new StringBuilder();
            sb.AppendLine("<Project>");
            sb.AppendLine("    <ItemGroup>");

            foreach (var project in projects)
            {
                var expandedProject = Environment.ExpandEnvironmentVariables(project.Project!);
                project.ProjectFullPath = Path.Combine(configRoot, expandedProject);

                if (!File.Exists(project.ProjectFullPath))
                {
                    throw new CommandException($"Failed to locate project: '{project.ProjectFullPath}'.");
                }

                sb.AppendLine($"        <MicrosoftTye_ProjectServices " +
                    $"Include=\"{project.ProjectFullPath}\" " +
                    $"Name=\"{project.Name}\" " +
                    $"BuildProperties=\"" +
                        $"{(project.BuildProperties.Any() ? project.BuildProperties.Select(kvp => $"{kvp.Name}={kvp.Value}").Aggregate((a, b) => a + ";" + b) : string.Empty)}" +
                    $"\" />");
            }
            sb.AppendLine(@"    </ItemGroup>");

            sb.AppendLine($@"    <Target Name=""MicrosoftTye_EvaluateProjects"">");
            sb.AppendLine($@"        <MsBuild Projects=""@(MicrosoftTye_ProjectServices)"" "
                + $@"Properties=""%(BuildProperties);"
                + $@"MicrosoftTye_ProjectName=%(Name)"" "
                + $@"Targets=""MicrosoftTye_GetProjectMetadata"" BuildInParallel=""true"" />");

            sb.AppendLine("    </Target>");
            sb.AppendLine("</Project>");
            File.WriteAllText(projectPath, sb.ToString());

            output.WriteDebugLine("Restoring and evaluating projects");

            var projectEvaluationTargets = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "ProjectEvaluation.targets");
            var msbuildEvaluationResult = await ProcessUtil.RunAsync(
                "dotnet",
                $"build  --no-restore " +
                    $"\"{projectPath}\" " +
                    // CustomAfterMicrosoftCommonTargets is imported by non-crosstargeting (single TFM) projects
                    @$"/p:CustomAfterMicrosoftCommonTargets=""{projectEvaluationTargets}"" " +
                    // CustomAfterMicrosoftCommonCrossTargetingTargets is imported by crosstargeting (multi-TFM) projects
                    // This ensures projects properties are evaluated correctly. However, multi-TFM projects must specify
                    // a specific TFM to build/run/publish and will otherwise throw an exception.
                    @$"/p:CustomAfterMicrosoftCommonCrossTargetingTargets=""{projectEvaluationTargets}"" " +
                    $"/nologo",
                throwOnError: false,
                workingDirectory: directory.DirectoryPath);

            // If the build fails, we're not really blocked from doing our work.
            // For now we just log the output to debug. There are errors that occur during
            // running these targets we don't really care as long as we get the data.
            if (msbuildEvaluationResult.ExitCode != 0)
            {
                output.WriteInfoLine($"Evaluating project failed with exit code {msbuildEvaluationResult.ExitCode}");
                output.WriteDebugLine($"Output: {msbuildEvaluationResult.StandardOutput}");
                output.WriteDebugLine($"Error: {msbuildEvaluationResult.StandardError}");
            }

            return msbuildEvaluationResult;
        }

        private static string? GetDockerFileContext(FileInfo source, ConfigService configService)
        {
            if (configService.DockerFileContext == null)
            {
                return null;
            }

            // On windows, calling docker build with an aboslute path that ends in a trailing slash fails,
            // but it's the exact opposite on linux, where it needs to have the trailing slash.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.TrimEndingDirectorySeparator(Path.Combine(source.DirectoryName!, configService.DockerFileContext));
            }
            else
            {
                var path = Path.Combine(source.DirectoryName!, configService.DockerFileContext);

                if (!Path.EndsInDirectorySeparator(path))
                {
                    return path += Path.DirectorySeparatorChar;
                }

                return path;
            }
        }

        private static ConfigApplication GetNestedConfig(ConfigApplication rootConfig, string? file)
        {
            var nestedConfig = ConfigFactory.FromFile(new FileInfo(file!));
            nestedConfig.Validate();

            if (nestedConfig.Name != rootConfig.Name)
            {
                throw new CommandException($"Nested configuration must have the same \"name\" in the tye.yaml. Root config: {rootConfig.Source}, nested config: {nestedConfig.Source}");
            }

            return nestedConfig;
        }

        private static void AddToRootServices(ApplicationBuilder root, HashSet<string> dependencies, string serviceName)
        {
            // Add ourselves in the set of all current dependencies.
            dependencies.Add(serviceName);

            // Iterate through all services and add the current services as a dependency (except ourselves)
            foreach (var s in root.Services)
            {
                if (dependencies.Contains(s.Name, StringComparer.OrdinalIgnoreCase)
                    && !s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                {
                    s.Dependencies.Add(serviceName);
                }
            }
        }

        private static ProbeBuilder GetProbeBuilder(ConfigProbe config) => new ProbeBuilder()
        {
            Http = config.Http != null ? GetHttpProberBuilder(config.Http) : null,
            InitialDelay = config.InitialDelay,
            Period = config.Period,
            Timeout = config.Timeout,
            SuccessThreshold = config.SuccessThreshold,
            FailureThreshold = config.FailureThreshold
        };

        private static HttpProberBuilder GetHttpProberBuilder(ConfigHttpProber config) => new HttpProberBuilder()
        {
            Path = config.Path,
            Headers = config.Headers,
            Port = config.Port,
            Protocol = config.Protocol
        };
    }
}
