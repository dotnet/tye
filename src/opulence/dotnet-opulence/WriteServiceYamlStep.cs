using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Opulence
{
    internal sealed class WriteServiceYamlStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Writing Manifests...";

        public bool Force { get; set; }

        public DirectoryInfo OutputDirectory { get; set; } = default!;

        public override Task ExecuteAsync(OutputContext output, Application application, ServiceEntry service)
        {
            var yaml = service.Outputs.OfType<IYamlManifestOutput>().ToArray();
            if (yaml.Length == 0)
            {
                output.WriteDebugLine($"No yaml manifests found for service '{service.FriendlyName}'. Skipping.");
                return Task.CompletedTask;
            }

            var outputFilePath = Path.Combine(OutputDirectory.FullName, $"{service.Service.Name}.yaml");
            output.WriteInfoLine($"Writing output to '{outputFilePath}'.");
            if (File.Exists(outputFilePath) && !Force)
            {
                throw new CommandException($"'{service.Service.Name}.yaml' already exists for project. use '--force' to overwrite.");
            }

            File.Delete(outputFilePath);

            using var stream = File.OpenWrite(outputFilePath);
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
            var yamlStream = new YamlStream(yaml.Select(y => y.Yaml));
            yamlStream.Save(writer, assignAnchors: false);

            return Task.CompletedTask;
        }
    }
}
