// Licensed to the .NET Foundation under one or more agreements.
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

            var config = ConfigFactory.FromFile(source);
            config.Validate();
            var builder = new ApplicationBuilder(source, config.Name ?? source.Directory.Name.ToLowerInvariant());
            if (!string.IsNullOrEmpty(config.Registry))
            {
                builder.Registry = new ContainerRegistry(config.Registry);
            }

            builder.Network = config.Network;

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

                builder.Extensions.Add(extension);
            }

            foreach (var configService in config.Services)
            {
                ServiceBuilder service;
                if (!string.IsNullOrEmpty(configService.Project))
                {
                    var expandedProject = Environment.ExpandEnvironmentVariables(configService.Project);
                    var projectFile = new FileInfo(Path.Combine(builder.Source.DirectoryName, expandedProject));
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
                else if (!string.IsNullOrEmpty(configService.Image))
                {
                    var container = new ContainerServiceBuilder(configService.Name!, configService.Image)
                    {
                        Args = configService.Args,
                        Replicas = configService.Replicas ?? 1
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
                        expandedExecutable = Path.GetFullPath(
                            Path.Combine(builder.Source.Directory.FullName, expandedExecutable));
                        workingDirectory = Path.GetDirectoryName(expandedExecutable)!;
                    }

                    var executable = new ExecutableServiceBuilder(configService.Name!, expandedExecutable)
                    {
                        Args = configService.Args,
                        WorkingDirectory =
                                             configService.WorkingDirectory != null
                                                 ? Path.GetFullPath(
                                                     Path.Combine(
                                                         builder.Source.Directory.FullName,
                                                         Environment.ExpandEnvironmentVariables(
                                                             configService.WorkingDirectory)))
                                                 : workingDirectory,
                        Replicas = configService.Replicas ?? 1
                    };
                    service = executable;
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

                builder.Services.Add(service);

                // If there are no bindings and we're in ASP.NET Core project then add an HTTP and HTTPS binding
                if (configService.Bindings.Count == 0 && service is ProjectServiceBuilder project2 && project2.IsAspNet)
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

                builder.Ingress.Add(ingress);

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
                        Service = configRule.Service,
                    };
                    ingress.Rules.Add(rule);
                }
            }

            return builder;
        }
    }
}
