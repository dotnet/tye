// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    public sealed class DeployServiceYamlStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Deploying Manifests...";

        public string Environment { get; set; } = "production";

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            var yaml = service.Outputs.OfType<IYamlManifestOutput>().ToArray();
            if (yaml.Length == 0)
            {
                output.WriteDebugLine($"No yaml manifests found for service '{service.Name}'. Skipping.");
                return;
            }

            if (!await KubectlDetector.Instance.IsKubectlInstalled.Value)
            {
                throw new CommandException($"Cannot apply manifests for '{service.Name}' because kubectl is not installed.");
            }

            if (!await KubectlDetector.Instance.IsKubectlConnectedToCluster.Value)
            {
                throw new CommandException($"Cannot apply manifests for '{service.Name}' because kubectl is not connected to a cluster.");
            }

            using var tempFile = TempFile.Create();
            output.WriteDebugLine($"Writing output to '{tempFile.FilePath}'.");

            {
                using var stream = File.OpenWrite(tempFile.FilePath);
                using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: -1, leaveOpen: true);
                var yamlStream = new YamlStream(yaml.Select(y => y.Yaml));
                yamlStream.Save(writer, assignAnchors: false);
            }

            // kubectl apply logic is implemented in the client in older versions of k8s. The capability
            // to get the same behavior in the server isn't present in every version that's relevant.
            //
            // https://kubernetes.io/docs/reference/using-api/api-concepts/#server-side-apply
            //
            output.WriteDebugLine("Running 'kubectl apply'.");
            output.WriteCommandLine("kubectl", $"apply -f \"{tempFile.FilePath}\"");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
                $"kubectl",
                $"apply -f \"{tempFile.FilePath}\"",
                System.Environment.CurrentDirectory,
                stdOut: capture.StdOut,
                stdErr: capture.StdErr);

            output.WriteDebugLine($"Done running 'kubectl apply' exit code: {exitCode}");
            if (exitCode != 0)
            {
                throw new CommandException("'kubectl apply' failed.");
            }

            output.WriteInfoLine($"Deployed service '{service.Name}'.");
        }
    }
}
