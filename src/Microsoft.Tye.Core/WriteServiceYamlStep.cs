// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    internal sealed class WriteServiceYamlStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Writing Manifests...";

        public bool Force { get; set; }

        public DirectoryInfo OutputDirectory { get; set; } = default!;

        public override Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            var yaml = service.Outputs.OfType<IYamlManifestOutput>().ToArray();
            if (yaml.Length == 0)
            {
                output.WriteDebugLine($"No yaml manifests found for service '{service.Name}'. Skipping.");
                return Task.CompletedTask;
            }

            var outputFilePath = Path.Combine(OutputDirectory.FullName, $"{service.Name}.yaml");
            output.WriteInfoLine($"Writing output to '{outputFilePath}'.");
            if (File.Exists(outputFilePath) && !Force)
            {
                throw new CommandException($"'{service.Name}.yaml' already exists for project. use '--force' to overwrite.");
            }

            File.Delete(outputFilePath);

            using var stream = File.OpenWrite(outputFilePath);
            using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: -1, leaveOpen: true);
            var yamlStream = new YamlStream(yaml.Select(y => y.Yaml));
            yamlStream.Save(writer, assignAnchors: false);

            return Task.CompletedTask;
        }
    }
}
