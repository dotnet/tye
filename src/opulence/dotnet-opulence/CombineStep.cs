using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Opulence
{
    internal sealed class CombineStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Compiling Services...";

        public string Environment { get; set; } = "production";

        public override Task ExecuteAsync(OutputContext output, Application application, ServiceEntry service)
        {
            // Process bindings and turn them into environment variables.
            foreach (var binding in service.Service.Bindings)
            {
                // Try to see if the other project is a service in this application. This can help with
                // heuristics if the binding doesn't have all possible info specified.
                var other = application.Services.FirstOrDefault(s => s.Service.Name == binding.Name);

                var key = $"SERVICES__{binding.Name}";

                // Find the value that needs to go in this env-var (in priority order).
                string value;
                if (binding.ConnectionString != null)
                {
                    binding.ConnectionString.Name ??= $"binding-{binding.Name}";

                    // It's a secret! We don't use env-vars for this.
                    continue;
                }
                else if (binding.Protocol != null)
                {
                    // This is fully specified as a URL.
                    value = ResolveUri(binding.Protocol, binding.Name, binding.Port);
                }
                else if (other?.Service.Source != null && other.AppliesToEnvironment(Environment))
                {
                    // If we get here it means that the other service is built from source.
                    // In this case we'll assume that it's http on 80 as a reasonable guess.
                    value = ResolveUri(other.Service.Protocol ?? "http", binding.Name, other.Service.Port);
                }
                else if (other?.Service.Source != null)
                {
                    // The other service is built from source, but doesn't apply to this environment.
                    // This is likely user-error.
                    throw new CommandException($"Unable to resolve the uri for binding '{binding.Name}'.");
                }
                else if (other?.Service.Source == null)
                {
                    // The service isn't built from source.
                    binding.ConnectionString = new Secret() { Name = $"binding-{binding.Name}", };

                    // It's a secret! We don't use env-vars for this.
                    continue;
                }
                else
                {
                    // Generic catch all case. We don't expect this to get hit because the three blocks
                    // above cover all possibilities, but the compiler doesn't agree.
                    throw new CommandException($"Unable to resolve the uri for binding '{binding.Name}'.");
                }

                service.Service.Environment[key] = value;
            }

            return Task.CompletedTask;
        }

        private static string ResolveUri(string protocol, string name, int? port)
        {
            if (protocol == "http" && (port == 80 || port == null))
            {
                return $"{protocol}://{name}";
            }
            else if (protocol == "https" && (port == 443 || port == null))
            {
                return $"{protocol}://{name}";
            }
            else if (port == null)
            {
                return $"{protocol}://{name}";
            }
            else
            {
                return $"{protocol}://{name}:{port}";
            }
        }
    }
}



