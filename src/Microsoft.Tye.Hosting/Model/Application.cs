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

                throw new InvalidOperationException("LOLNO");
            }

            void SetBinding(string serviceName, ServiceBinding b)
            {
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

                if (!string.IsNullOrEmpty(b.ConnectionString))
                {
                    // Special case for connection strings
                    set($"CONNECTIONSTRING__{configName}", b.ConnectionString);
                }

                if (!string.IsNullOrEmpty(b.Protocol))
                {
                    // IConfiguration specific (double underscore ends up telling the configuration provider to use it as a separator)
                    set($"SERVICE__{configName}__PROTOCOL", b.Protocol);
                    set($"{envName}_SERVICE_PROTOCOL", b.Protocol);
                }

                if (b.Port != null)
                {
                    set($"SERVICE__{configName}__PORT", b.Port.Value.ToString());
                    set($"{envName}_SERVICE_PORT", b.Port.Value.ToString());
                }

                set($"SERVICE__{configName}__HOST", b.Host ?? defaultHost);
                set($"{envName}_SERVICE_HOST", b.Host ?? defaultHost);
            }

            // Inject dependency information
            foreach (var s in Services.Values)
            {
                foreach (var b in s.Description.Bindings)
                {
                    SetBinding(s.Description.Name.ToUpper(), b);
                }
            }
        }
    }
}
