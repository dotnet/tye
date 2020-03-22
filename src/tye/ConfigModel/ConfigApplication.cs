// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Tye.Hosting.Model;
using YamlDotNet.Serialization;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigApplication
    {
        // This gets set by all of the code paths that read the application
        [YamlIgnore]
        public FileInfo Source { get; set; } = default!;

        public string? Name { get; set; }

        public string? Registry { get; set; }

        public List<ConfigIngress> Ingress { get; set; } = new List<ConfigIngress>();

        public List<ConfigService> Services { get; set; } = new List<ConfigService>();

        public Tye.Hosting.Model.Application ToHostingApplication()
        {
            var services = new Dictionary<string, Tye.Hosting.Model.Service>();
            foreach (var service in Services)
            {
                RunInfo? runInfo;
                if (service.External)
                {
                    runInfo = null;
                }
                else if (service.Image is object)
                {
                    var dockerRunInfo = new DockerRunInfo(service.Image, service.Args);

                    foreach (var mapping in service.Volumes)
                    {
                        dockerRunInfo.VolumeMappings[mapping.Source!] = mapping.Target!;
                    }

                    runInfo = dockerRunInfo;
                }
                else if (service.Executable is object)
                {
                    runInfo = new ExecutableRunInfo(service.Executable, service.WorkingDirectory, service.Args);
                }
                else if (service.Project is object)
                {
                    var projectInfo = new ProjectRunInfo(service.Project, service.Args, service.Build ?? true);

                    foreach (var mapping in service.Volumes)
                    {
                        projectInfo.VolumeMappings[mapping.Source!] = mapping.Target!;
                    }

                    runInfo = projectInfo;
                }
                else
                {
                    throw new InvalidOperationException($"Cannot figure out how to run service '{service.Name}'.");
                }

                var description = new Tye.Hosting.Model.ServiceDescription(service.Name, runInfo)
                {
                    Replicas = service.Replicas ?? 1,
                };

                foreach (var binding in service.Bindings)
                {
                    description.Bindings.Add(new Tye.Hosting.Model.ServiceBinding()
                    {
                        ConnectionString = binding.ConnectionString,
                        Host = binding.Host,
                        AutoAssignPort = binding.AutoAssignPort,
                        InternalPort = binding.InternalPort,
                        Name = binding.Name,
                        Port = binding.Port,
                        Protocol = binding.Protocol,
                    });
                }

                foreach (var entry in service.Configuration)
                {
                    description.Configuration.Add(new ConfigurationSource(entry.Name, entry.Value));
                }

                services.Add(service.Name, new Tye.Hosting.Model.Service(description));
            }

            foreach (var ingress in Ingress)
            {
                var rules = new List<IngressRule>();

                foreach (var rule in ingress.Rules)
                {
                    rules.Add(new IngressRule(rule.Host, rule.Path, rule.Service!));
                }

                var runInfo = new IngressRunInfo(rules);

                var description = new Tye.Hosting.Model.ServiceDescription(ingress.Name, runInfo)
                {
                    Replicas = ingress.Replicas ?? 1,
                };

                foreach (var binding in ingress.Bindings)
                {
                    description.Bindings.Add(new Tye.Hosting.Model.ServiceBinding()
                    {
                        AutoAssignPort = binding.AutoAssignPort,
                        Name = binding.Name,
                        Port = binding.Port,
                        Protocol = binding.Protocol,
                    });
                }

                services.Add(ingress.Name, new Tye.Hosting.Model.Service(description));
            }

            return new Tye.Hosting.Model.Application(Source, services);
        }
    }
}
