// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.ConfigModel
{
    public static class ApplicationBuilderExtensions
    {
        public static Application ToHostingApplication(this ApplicationBuilder application)
        {
            var services = new Dictionary<string, Service>();
            foreach (var service in application.Services)
            {
                RunInfo? runInfo;
                int replicas;
                var env = new List<ConfigurationSource>();
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
                        dockerRunInfo.VolumeMappings[mapping.Source!] = mapping.Target!;
                    }

                    runInfo = dockerRunInfo;
                    replicas = container.Replicas;

                    foreach (var entry in container.EnvironmentVariables)
                    {
                        env.Add(new ConfigurationSource(entry.Name, entry.Value));
                    }
                }
                else if (service is ExecutableServiceBuilder executable)
                {
                    runInfo = new ExecutableRunInfo(executable.Executable, executable.WorkingDirectory, executable.Args);
                    replicas = executable.Replicas;

                    foreach (var entry in executable.EnvironmentVariables)
                    {
                        env.Add(new ConfigurationSource(entry.Name, entry.Value));
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
                        projectInfo.VolumeMappings[mapping.Source!] = mapping.Target!;
                    }

                    runInfo = projectInfo;
                    replicas = project.Replicas;

                    foreach (var entry in project.EnvironmentVariables)
                    {
                        env.Add(new ConfigurationSource(entry.Name, entry.Value));
                    }
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
                    description.Bindings.Add(new Hosting.Model.ServiceBinding()
                    {
                        ConnectionString = binding.ConnectionString,
                        Host = binding.Host,
                        AutoAssignPort = binding.AutoAssignPort,
                        ContainerPort = binding.ContainerPort,
                        Name = binding.Name,
                        Port = binding.Port,
                        Protocol = binding.Protocol,
                    });
                }

                services.Add(service.Name, new Service(description));
            }

            // Ingress get turned into services for hosting
            foreach (var ingress in application.Ingress)
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
                    description.Bindings.Add(new Hosting.Model.ServiceBinding()
                    {
                        AutoAssignPort = binding.AutoAssignPort,
                        Name = binding.Name,
                        Port = binding.Port,
                        Protocol = binding.Protocol,
                    });
                }

                services.Add(ingress.Name, new Service(description));
            }

            return new Application(application.Source, services);
        }
    }
}
