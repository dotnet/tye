// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class GenerateApplicationKubernetesManifestStep : ApplicationExecutor.ApplicationStep
    {
        public override string DisplayText => "Generating Application Manifests...";

        public string Environment { get; set; } = "production";

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application)
        {
            var outputFilePath = Path.GetFullPath(Path.Combine(application.Source.DirectoryName, $"{application.Name}-generate-{Environment}.yaml"));
            output.WriteInfoLine($"Writing output to '{outputFilePath}'.");
            {
                File.Delete(outputFilePath);

                await using var stream = File.OpenWrite(outputFilePath);
                await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                await ApplicationYamlWriter.WriteAsync(output, writer, application);
            }
        }
    }
}
