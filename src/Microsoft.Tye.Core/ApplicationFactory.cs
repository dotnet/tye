﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Tye.ConfigModel;

namespace Microsoft.Tye
{
    public static class ApplicationFactory
    {
        public static async Task<ApplicationBuilder> CreateAsync(OutputContext output, FileInfo source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var queue = new Queue<(ConfigApplication, HashSet<string>)>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootConfig = ConfigFactory.FromFile(source);
            rootConfig.Validate();

            var root = new ApplicationBuilder(source, rootConfig.Name ?? source.Directory.Name.ToLowerInvariant());
            root.Namespace = rootConfig.Namespace;

            queue.Enqueue((rootConfig, new HashSet<string>()));

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                var config = item.Item1;

                // dependencies represents a set of all dependencies
                var dependencies = item.Item2;
                if (!visited.Add(config.Source.FullName))
                {
                    continue;
                }

                if (config == rootConfig && !string.IsNullOrEmpty(config.Registry))
                {
                    root.Registry = new ContainerRegistry(config.Registry);
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

                foreach (var configService in config.Services)
                {
                    ServiceBuilder service;
                    if (root.Services.Any(s => s.Name == configService.Name))
                    {
                        // Even though this service has already created a service, we still need
                        // to update dependency information
                        AddToRootServices(root, dependencies, configService.Name);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(configService.Project))
                    {
                        var expandedProject = Environment.ExpandEnvironmentVariables(configService.Project);
                        var projectFile = new FileInfo(Path.Combine(config.Source.DirectoryName, expandedProject));
                        var project = new ProjectServiceBuilder(configService.Name!, projectFile);
                        service = project;

                        project.Build = configService.Build ?? true;
                        project.Args = configService.Args;
                        foreach (var buildProperty in configService.BuildProperties)
                        {
                            project.BuildProperties.Add(buildProperty.Name, buildProperty.Value);
                        }
                        project.Replicas = configService.Replicas ?? 1;

                        await ProjectReader.ReadProjectDetailsAsync(output, project);

                        // We don't apply more container defaults here because we might need
                        // to prompt for the registry name.
                        project.ContainerInfo = new ContainerInfo() { UseMultiphaseDockerfile = false, };

                        // Do k8s by default.
                        project.ManifestInfo = new KubernetesManifestInfo();
                    }
                    else if (!string.IsNullOrEmpty(configService.Image) || !string.IsNullOrEmpty(configService.DockerFile))
                    {
                        var container = new ContainerServiceBuilder(configService.Name!, configService.Image!)
                        {
                            Args = configService.Args,
                            Replicas = configService.Replicas ?? 1,
                            DockerFile = configService.DockerFile != null ? Path.Combine(source.DirectoryName, configService.DockerFile) : null,
                            DockerFileContext = configService.DockerFileContext != null ? Path.Combine(source.DirectoryName, configService.DockerFileContext) : null
                        };
                        service = container;
                    }
                    else if (!string.IsNullOrEmpty(configService.Executable))
                    {
                        var expandedExecutable = Environment.ExpandEnvironmentVariables(configService.Executable);
                        var workingDirectory = "";

                        // Special handling of .dlls as executables (it will be executed as dotnet {dll})
                        if (Path.GetExtension(expandedExecutable) == ".dll")
                        {
                            expandedExecutable = Path.GetFullPath(Path.Combine(config.Source.Directory.FullName, expandedExecutable));
                            workingDirectory = Path.GetDirectoryName(expandedExecutable)!;
                        }

                        var executable = new ExecutableServiceBuilder(configService.Name!, expandedExecutable)
                        {
                            Args = configService.Args,
                            WorkingDirectory = configService.WorkingDirectory != null ?
                            Path.GetFullPath(Path.Combine(config.Source.Directory.FullName, Environment.ExpandEnvironmentVariables(configService.WorkingDirectory))) :
                            workingDirectory,
                            Replicas = configService.Replicas ?? 1
                        };
                        service = executable;
                    }
                    else if (!string.IsNullOrEmpty(configService.Include))
                    {
                        var expandedYaml = Environment.ExpandEnvironmentVariables(configService.Include);

                        var nestedConfig = GetNestedConfig(rootConfig, Path.Combine(config.Source.DirectoryName, expandedYaml));
                        queue.Enqueue((nestedConfig, new HashSet<string>()));

                        AddToRootServices(root, dependencies, configService.Name);
                        continue;
                    }
                    else if (!string.IsNullOrEmpty(configService.Repository))
                    {
                        // clone to .tye folder
                        var path = Path.Join(rootConfig.Source.DirectoryName, ".tye", "deps");
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }

                        var clonePath = Path.Combine(path, configService.Name);

                        if (!Directory.Exists(clonePath))
                        {
                            if (!await GitDetector.Instance.IsGitInstalled.Value)
                            {
                                throw new CommandException($"Cannot clone repository {configService.Repository} because git is not installed. Please install git if you'd like to use \"repository\" in tye.yaml.");
                            }

                            var result = await ProcessUtil.RunAsync("git", $"clone {configService.Repository} {clonePath}", workingDirectory: path, throwOnError: false);

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
                        var external = new ExternalServiceBuilder(configService.Name!);
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

                foreach (var configIngress in config.Ingress)
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
                        };
                        ingress.Bindings.Add(binding);
                    }

                    foreach (var configRule in configIngress.Rules)
                    {
                        var rule = new IngressRuleBuilder()
                        {
                            Host = configRule.Host,
                            Path = configRule.Path,
                            Service = configRule.Service!, // validated elsewhere
                        };
                        ingress.Rules.Add(rule);
                    }
                }
            }

            return root;
        }

        private static ConfigApplication GetNestedConfig(ConfigApplication rootConfig, string? file)
        {
            var nestedConfig = ConfigFactory.FromFile(new FileInfo(file));
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
    }
}
