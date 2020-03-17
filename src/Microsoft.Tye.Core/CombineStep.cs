// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class CombineStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Compiling Services...";

        public string Environment { get; set; } = "production";

        public override Task ExecuteAsync(OutputContext output, Application application, ServiceEntry service)
        {
            // No need to do this computation for a non-project since we're not deploying it.
            if (!(service.Service.Source is Project))
            {
                return Task.CompletedTask;
            }

            // Compute ASPNETCORE_URLS based on the bindings exposed by *this* project.
            foreach (var binding in service.Service.Bindings)
            {
                if (binding.Protocol == "http" || binding.Protocol == null)
                {
                    var port = binding.Port ?? 80;
                    var urls = $"http://*{(port == 80 ? "" : (":" + port.ToString()))}";
                    service.Service.Environment.Add("ASPNETCORE_URLS", urls);
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

                foreach (var binding in other.Service.Bindings)
                {
                    // The other thing is a project, and will be deployed along with this
                    // service.
                    var configName = binding.Name == other.Service.Name ? other.Service.Name.ToUpperInvariant() : $"{other.Service.Name.ToUpperInvariant()}__{binding.Name.ToUpperInvariant()}";
                    if (other.Service.Source is Project)
                    {
                        if (!string.IsNullOrEmpty(binding.ConnectionString))
                        {
                            // Special case for connection strings
                            bindings.Bindings.Add(new EnvironmentVariableInputBinding($"CONNECTIONSTRING__{configName}", binding.ConnectionString));
                        }

                        if (binding.Protocol == "https")
                        {
                            // We skip https for now in deployment, because the E2E requires certificates
                            // and we haven't done those features yet.
                            continue;
                        }

                        if (!string.IsNullOrEmpty(binding.Protocol))
                        {
                            bindings.Bindings.Add(new EnvironmentVariableInputBinding($"SERVICE__{configName}__PROTOCOL", binding.Protocol));
                        }

                        if (binding.Port != null)
                        {
                            bindings.Bindings.Add(new EnvironmentVariableInputBinding($"SERVICE__{configName}__PORT", binding.Port.Value.ToString()));
                        }

                        bindings.Bindings.Add(new EnvironmentVariableInputBinding($"SERVICE__{configName}__HOST", binding.Host ?? other.Service.Name));
                    }
                    else
                    {
                        // The other service is not a project, so we'll use secrets.
                        bindings.Bindings.Add(new SecretInputBinding(
                            name: $"binding-{Environment}-{other.Service.Name}-{binding.Name}-secret",
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



