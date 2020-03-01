using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Opulence
{
    public sealed class ApplicationYamlWriter
    {
        public static Task WriteAsync(OutputContext output, StreamWriter writer, Application application)
        {
            var yaml = application.Services.SelectMany(s => s.Outputs.OfType<IYamlManifestOutput>()).ToArray();
            if (yaml.Length == 0)
            {
                output.WriteDebugLine($"No yaml manifests found. Skipping.");
                return Task.CompletedTask;
            }

            var yamlStream = new YamlStream(yaml.Select(y => y.Yaml));
            yamlStream.Save(writer, assignAnchors: false);

            return Task.CompletedTask;
        }
    }
}
