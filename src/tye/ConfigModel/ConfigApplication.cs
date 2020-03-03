using System;
using System.Collections.Generic;
using System.IO;
using Tye.Hosting.Model;
using YamlDotNet.Serialization;

namespace Tye.ConfigModel
{
    internal class ConfigApplication
    {
        // This gets set by all of the code paths that read the application
        [YamlIgnore]
        public FileInfo Source { get; set; } = default!;

        public string? Name { get; set; }

        public string? Registry { get; set; }

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
                else if (service.DockerImage is object)
                {
                    runInfo = new DockerRunInfo(service.DockerImage, service.Args);
                }
                else if (service.Executable is object)
                {
                    runInfo = new ExecutableRunInfo(service.Executable, service.WorkingDirectory, service.Args);
                }
                else if (service.Project is object)
                {
                    runInfo = new ProjectRunInfo(service.Project, service.Args, service.Build ?? true);
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

            return new Tye.Hosting.Model.Application(Source, services);
        }
    }
}
