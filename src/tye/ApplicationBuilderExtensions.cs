// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Tye.Extensions;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye
{
    public static class ApplicationBuilderExtensions
    {
        // For layering reasons this has to live in the `tye` project. We don't want to leak
        // the extensions themselves into Tye.Core.
        public static async Task ProcessExtensionsAsync(this ApplicationBuilder application, OutputContext output, ExtensionContext.OperationKind operation)
        {
            foreach (var extensionConfig in application.Extensions)
            {
                if (!WellKnownExtensions.Extensions.TryGetValue(extensionConfig.Name, out var extension))
                {
                    throw new CommandException($"Could not find the extension '{extensionConfig.Name}'.");
                }

                var context = new ExtensionContext(application, output, operation);
                await extension.ProcessAsync(context, extensionConfig);
            }
        }

        public static Application ToHostingApplication(this ApplicationBuilder application)
        {
            // This needs to allow nesting, only add services in top level.
            var rootServices = new Dictionary<string, Service>();
            var queue = new Queue<(ApplicationBuilder, Dictionary<string, Service>)>();
            queue.Enqueue((application, rootServices));

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                var app = item.Item1;
                var services = item.Item2;
                foreach (var service in app.Services)
                {
                    RunInfo? runInfo;
                    int replicas;
                    var env = new List<EnvironmentVariable>();
                    if (service is ExternalServiceBuilder)
                    {
                        runInfo = null;
                        replicas = 1;
                    }
                    else if (service is ContainerServiceBuilder container)
                    {
                        var dockerRunInfo = new DockerRunInfo(container.Image, container.Args);

                        foreach (var mapping in container.Volumes)
                        {
                            dockerRunInfo.VolumeMappings.Add(new DockerVolume(mapping.Source, mapping.Name, mapping.Target));
                        }

                        runInfo = dockerRunInfo;
                        replicas = container.Replicas;

                        foreach (var entry in container.EnvironmentVariables)
                        {
                            env.Add(entry.ToHostingEnvironmentVariable());
                        }
                    }
                    else if (service is ExecutableServiceBuilder executable)
                    {
                        runInfo = new ExecutableRunInfo(executable.Executable, executable.WorkingDirectory, executable.Args);
                        replicas = executable.Replicas;

                        foreach (var entry in executable.EnvironmentVariables)
                        {
                            env.Add(entry.ToHostingEnvironmentVariable());
                        }
                    }
                    else if (service is ProjectServiceBuilder project)
                    {
                        if (project.TargetFrameworks.Length > 1)
                        {
                            throw new InvalidOperationException($"Unable to run {project.Name}. Multi-targeted projects are not supported.");
                        }

                        if (project.RunCommand == null)
                        {
                            throw new InvalidOperationException($"Unable to run {project.Name}. The project does not have a run command");
                        }

                        var projectInfo = new ProjectRunInfo(project);

                        foreach (var mapping in project.Volumes)
                        {
                            projectInfo.VolumeMappings.Add(new DockerVolume(mapping.Source, mapping.Name, mapping.Target));
                        }

                        runInfo = projectInfo;
                        replicas = project.Replicas;

                        foreach (var entry in project.EnvironmentVariables)
                        {
                            env.Add(entry.ToHostingEnvironmentVariable());
                        }
                    }
                    else if (service is TyeYamlServiceBuilder tyeYaml)
                    {
                        // putting this inside of tyeyamlruninfo isn't great.
                        // make part of the application.
                        var innerServices = new Dictionary<string, Service>();
                        queue.Enqueue((tyeYaml.Builder, innerServices));
                        runInfo = new TyeYamlRunInfo(tyeYaml, innerServices);
                        replicas = 1;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot figure out how to run service '{service.Name}'.");
                    }

                    var description = new ServiceDescription(service.Name, runInfo)
                    {
                        Replicas = replicas,
                    };
                    description.Configuration.AddRange(env);

                    foreach (var binding in service.Bindings)
                    {
                        description.Bindings.Add(new ServiceBinding()
                        {
                            ConnectionString = binding.ConnectionString,
                            Host = binding.Host,
                            ContainerPort = binding.ContainerPort,
                            Name = binding.Name,
                            Port = binding.Port,
                            Protocol = binding.Protocol,
                        });
                    }

                    services.Add(service.Name, new Service(description));
                }

                // Ingress get turned into services for hosting
                foreach (var ingress in app.Ingress)
                {
                    var rules = new List<IngressRule>();

                    foreach (var rule in ingress.Rules)
                    {
                        rules.Add(new IngressRule(rule.Host, rule.Path, rule.Service!));
                    }

                    var runInfo = new IngressRunInfo(rules);

                    var description = new ServiceDescription(ingress.Name, runInfo)
                    {
                        Replicas = ingress.Replicas,
                    };

                    foreach (var binding in ingress.Bindings)
                    {
                        description.Bindings.Add(new ServiceBinding()
                        {
                            Name = binding.Name,
                            Port = binding.Port,
                            Protocol = binding.Protocol,
                        });
                    }

                    services.Add(ingress.Name, new Service(description));
                }
            }

            return new Application(application.Source, rootServices) { Network = application.Network };
        }

        public static Tye.Hosting.Model.EnvironmentVariable ToHostingEnvironmentVariable(this EnvironmentVariableBuilder builder)
        {
            var env = new Tye.Hosting.Model.EnvironmentVariable(builder.Name);
            env.Value = builder.Value;
            if (builder.Source != null)
            {
                env.Source = new Tye.Hosting.Model.EnvironmentVariableSource(builder.Source.Service, builder.Source.Binding);
                env.Source.Kind = (Tye.Hosting.Model.EnvironmentVariableSource.SourceKind)(int)builder.Source.Kind;
            }

            return env;
        }
    }
}
