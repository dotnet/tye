// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Tye.Hosting.Model
{
    public class Application
    {
        public Application(FileInfo source, Dictionary<string, Service> services)
        {
            Source = source.FullName;
            ContextDirectory = source.DirectoryName;
            Services = services;
        }

        public string Source { get; }

        public string ContextDirectory { get; }

        public Dictionary<string, Service> Services { get; }

        public Dictionary<object, object> Items { get; } = new Dictionary<object, object>();

        public string? Network { get; set; }

        public void PopulateEnvironment(Service service, Action<string, string> set, string defaultHost = "localhost")
        {
            if (service.Description.Configuration != null)
            {
                // Inject normal configuration
                foreach (var pair in service.Description.Configuration)
                {
                    if (pair.Value is object)
                    {
                        set(pair.Name, pair.Value);
                    }
                    else if (pair.Source is object)
                    {
                        set(pair.Name, GetValueFromBinding(pair.Source));
                    }
                }
            }

            string GetValueFromBinding(EnvironmentVariableSource source)
            {
                if (!Services.TryGetValue(source.Service, out var service))
                {
                    throw new InvalidOperationException($"Could not find service '{source.Service}'.");
                }

                var binding = service.Description.Bindings.Where(b => b.Name == source.Binding).FirstOrDefault();
                if (binding == null)
                {
                    throw new InvalidOperationException($"Could not find binding '{source.Binding}' for service '{source.Service}'.");
                }

                // TODO finish
                if (source.Kind == EnvironmentVariableSource.SourceKind.Port && binding.Port != null)
                {
                    return binding.Port.Value.ToString();
                }
                else if (source.Kind == EnvironmentVariableSource.SourceKind.Host)
                {
                    return binding.Host ?? defaultHost;
                }
                else if (source.Kind == EnvironmentVariableSource.SourceKind.Url)
                {
                    return $"{binding.Protocol ?? "http"}://{binding.Host ?? defaultHost}" + (binding.Port.HasValue ? (":" + binding.Port) : string.Empty);
                }
                else if (source.Kind == EnvironmentVariableSource.SourceKind.ConnectionString && binding.ConnectionString != null)
                {
                    return binding.ConnectionString;
                }

                throw new InvalidOperationException($"Unable to resolve the desired value '{source.Kind}' from binding '{source.Binding}' for service '{source.Service}'.");
            }

            void SetBinding(ServiceDescription targetService, ServiceBinding b)
            {
                var serviceName = targetService.Name.ToUpper();
                var configName = "";
                var envName = "";

                if (string.IsNullOrEmpty(b.Name))
                {
                    configName = serviceName;
                    envName = serviceName;
                }
                else
                {
                    configName = $"{serviceName.ToUpper()}__{b.Name.ToUpper()}";
                    envName = $"{serviceName.ToUpper()}_{b.Name.ToUpper()}";
                }

                // Use the container name as the host name if there's a single replica (current limitation)
                var host = b.Host ?? (service.Description.RunInfo is DockerRunInfo ? targetService.Name : defaultHost);

                // Review: should we split this codepath based on the KIND of service that 
                // provides the binding?

                // Create the best kind of connection string possible, in order:
                //
                // - connectionString from config
                // - URI
                // - host:port
                //
                // This is used by the GetConnectionString method, commonly when the service providing the
                // binding isn't the user's code.
                if (!string.IsNullOrEmpty(b.ConnectionString))
                {
                    // Special case for connection strings
                    set($"CONNECTIONSTRINGS__{configName}", b.ConnectionString);
                }
                else if (!string.IsNullOrEmpty(b.Protocol) && b.Port != null)
                {
                    set($"CONNECTIONSTRINGS__{configName}", $"{b.Protocol}://{host}:{b.Port}");
                }
                else if (b.Port != null)
                {
                    set($"CONNECTIONSTRINGS__{configName}", $"{host}:{b.Port}");
                }

                if (!string.IsNullOrEmpty(b.Protocol))
                {
                    // IConfiguration specific (double underscore ends up telling the configuration provider to use it as a separator)
                    set($"SERVICE__{configName}__PROTOCOL", b.Protocol);
                    set($"{envName}_SERVICE_PROTOCOL", b.Protocol);
                }

                if (b.Port != null)
                {
                    var port = (service.Description.RunInfo is DockerRunInfo) ? b.ContainerPort ?? b.Port.Value : b.Port.Value;

                    set($"SERVICE__{configName}__PORT", port.ToString());
                    set($"{envName}_SERVICE_PORT", port.ToString());
                }

                set($"SERVICE__{configName}__HOST", host);
                set($"{envName}_SERVICE_HOST", host);
            }

            // Inject dependency information
            foreach (var s in Services.Values)
            {
                foreach (var b in s.Description.Bindings)
                {
                    SetBinding(s.Description, b);
                }
            }
        }
    }
}
