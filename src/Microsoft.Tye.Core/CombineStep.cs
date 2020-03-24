// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class CombineStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Compiling Services...";

        public string Environment { get; set; } = "production";

        public override Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            // No need to do this computation for a non-project since we're not deploying it.
            if (!(service is ProjectServiceBuilder project))
            {
                return Task.CompletedTask;
            }

            // Compute ASPNETCORE_URLS based on the bindings exposed by *this* project.
            foreach (var binding in service.Bindings)
            {
                if (binding.Protocol == null && binding.ConnectionString == null)
                {
                    binding.Protocol = "http";
                }

                if (binding.AutoAssignPort && binding.Port == null && binding.Protocol == "http")
                {
                    binding.Port = 80;
                }

                if (binding.Protocol == "http")
                {
                    var port = binding.Port ?? 80;
                    var urls = $"http://*{(port == 80 ? "" : (":" + port.ToString()))}";
                    project.EnvironmentVariables.Add(new EnvironmentVariable("ASPNETCORE_URLS", urls));
                    project.EnvironmentVariables.Add(new EnvironmentVariable("PORT", port.ToString(CultureInfo.InvariantCulture)));
                    break;
                }
            }

            // Process bindings and turn them into environment variables and secrets. There's
            // some duplication with the code in m8s (Application.cs) for populating environments.
            //
            // service.Service.Bindings is the bindings OUT - this step computes bindings IN.
            var bindings = new ComputedBindings();
            service.Outputs.Add(bindings);

            foreach (var other in application.Services)
            {
                if (object.ReferenceEquals(service, other))
                {
                    continue;
                }

                foreach (var binding in other.Bindings)
                {
                    // The other thing is a project, and will be deployed along with this
                    // service.
                    var configName =
                        (binding.Name is null || binding.Name == other.Name) ?
                        other.Name.ToUpperInvariant() :
                        $"{other.Name.ToUpperInvariant()}__{binding.Name.ToUpperInvariant()}";
                    if (other is ProjectServiceBuilder)
                    {
                        if (!string.IsNullOrEmpty(binding.ConnectionString))
                        {
                            // Special case for connection strings
                            bindings.Bindings.Add(new EnvironmentVariableInputBinding($"CONNECTIONSTRING__{configName}", binding.ConnectionString));
                            continue;
                        }

                        if (binding.Protocol == "https")
                        {
                            // We skip https for now in deployment, because the E2E requires certificates
                            // and we haven't done those features yet.
                            continue;
                        }

                        if (binding.Protocol == null)
                        {
                            binding.Protocol = "http";
                        }

                        if (binding.AutoAssignPort && binding.Port == null && binding.Protocol == "http")
                        {
                            binding.Port = 80;
                        }

                        if (!string.IsNullOrEmpty(binding.Protocol))
                        {
                            bindings.Bindings.Add(new EnvironmentVariableInputBinding($"SERVICE__{configName}__PROTOCOL", binding.Protocol));
                        }

                        if (binding.Port != null)
                        {
                            bindings.Bindings.Add(new EnvironmentVariableInputBinding($"SERVICE__{configName}__PORT", binding.Port.Value.ToString()));
                        }

                        bindings.Bindings.Add(new EnvironmentVariableInputBinding($"SERVICE__{configName}__HOST", binding.Host ?? other.Name));
                    }
                    else
                    {
                        // The other service is not a project, so we'll use secrets.
                        bindings.Bindings.Add(new SecretInputBinding(
                            name: $"binding-{Environment}-{other.Name}-{binding.Name ?? other.Name}-secret",
                            filename: $"CONNECTIONSTRING__{configName}",
                            other,
                            binding));
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}



