// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
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
            var bindings = ComputeBindings(service, defaultHost);

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
                        set(pair.Name, GetValueFromBinding(bindings, pair.Source));
                    }
                }
            }

            // Inject dependency information
            foreach (var binding in bindings)
            {
                SetBinding(binding);
            }

            static string GetValueFromBinding(List<EffectiveBinding> bindings, EnvironmentVariableSource source)
            {
                var binding = bindings.Where(b => b.Service == source.Service && b.Name == source.Binding).FirstOrDefault();
                if (binding == null)
                {
                    throw new InvalidOperationException($"Could not find binding '{source.Binding}' for service '{source.Service}'.");
                }

                if (source.Kind == EnvironmentVariableSource.SourceKind.Port && binding.Port != null)
                {
                    return binding.Port.Value.ToString();
                }
                else if (source.Kind == EnvironmentVariableSource.SourceKind.Host)
                {
                    return binding.Host;
                }
                else if (source.Kind == EnvironmentVariableSource.SourceKind.Url)
                {
                    return $"{binding.Protocol ?? "http"}://{binding.Host}" + (binding.Port.HasValue ? (":" + binding.Port) : string.Empty);
                }
                else if (source.Kind == EnvironmentVariableSource.SourceKind.ConnectionString && binding.ConnectionString != null)
                {
                    return binding.ConnectionString;
                }

                throw new InvalidOperationException($"Unable to resolve the desired value '{source.Kind}' from binding '{source.Binding}' for service '{source.Service}'.");
            }

            void SetBinding(EffectiveBinding binding)
            {
                var serviceName = binding.Service.ToUpper();
                var configName = "";
                var envName = "";

                if (string.IsNullOrEmpty(binding.Name))
                {
                    configName = serviceName;
                    envName = serviceName;
                }
                else
                {
                    configName = $"{serviceName.ToUpper()}__{binding.Name.ToUpper()}";
                    envName = $"{serviceName.ToUpper()}_{binding.Name.ToUpper()}";
                }

                if (!string.IsNullOrEmpty(binding.ConnectionString))
                {
                    // Special case for connection strings
                    var connectionString = TokenReplacement.ReplaceValues(binding.ConnectionString, binding, bindings);
                    set($"CONNECTIONSTRINGS__{configName}", connectionString);
                    return;
                }

                if (!string.IsNullOrEmpty(binding.Protocol))
                {
                    // IConfiguration specific (double underscore ends up telling the configuration provider to use it as a separator)
                    set($"SERVICE__{configName}__PROTOCOL", binding.Protocol);
                    set($"{envName}_SERVICE_PROTOCOL", binding.Protocol);
                }

                if (binding.Port != null)
                {
                    set($"SERVICE__{configName}__PORT", binding.Port.Value.ToString(CultureInfo.InvariantCulture));
                    set($"{envName}_SERVICE_PORT", binding.Port.Value.ToString(CultureInfo.InvariantCulture));
                }

                set($"SERVICE__{configName}__HOST", binding.Host);
                set($"{envName}_SERVICE_HOST", binding.Host);
            }
        }

        // Compute the list of bindings visible to `service`. This is contextual because details like
        // whether `service` is a container or not will change the result.
        private List<EffectiveBinding> ComputeBindings(Service service, string defaultHost)
        {
            var bindings = new List<EffectiveBinding>();

            var isDockerRunInfo = service.Description.RunInfo is DockerRunInfo;
            GetEffectiveBindings(isDockerRunInfo, defaultHost, bindings, service);

            foreach (var serv in service.Description.Dependencies)
            {
                GetEffectiveBindings(isDockerRunInfo, defaultHost, bindings, Services[serv]);
            }

            return bindings;
        }

        private static void GetEffectiveBindings(bool isDockerRunInfo, string defaultHost, List<EffectiveBinding> bindings, Service service)
        {
            foreach (var b in service.Description.Bindings)
            {
                var protocol = b.Protocol;
                var host = isDockerRunInfo ? service.Description.Name : defaultHost;

                var port = b.Port;
                if (b.Port is object && isDockerRunInfo)
                {
                    port = b.ContainerPort ?? b.Port.Value;
                }

                bindings.Add(new EffectiveBinding(
                    service.Description.Name,
                    b.Name,
                    protocol,
                    host,
                    port,
                    b.ConnectionString,
                    service.Description.Configuration));
            }
        }
    }
}
