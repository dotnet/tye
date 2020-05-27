// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class DeployApplicationKubernetesManifestStep : ApplicationExecutor.ApplicationStep
    {
        public override string DisplayText => "Deploying Application Manifests...";

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application)
        {
            using var step = output.BeginStep("");

            if (!await KubectlDetector.Instance.IsKubectlInstalled.Value)
            {
                throw new CommandException($"Cannot apply manifests because kubectl is not installed.");
            }

            if (!await KubectlDetector.Instance.IsKubectlConnectedToCluster.Value)
            {
                throw new CommandException($"Cannot apply manifests because kubectl is not connected to a cluster.");
            }

            using var tempFile = TempFile.Create();
            output.WriteInfoLine($"Writing output to '{tempFile.FilePath}'.");

            {
                await using var stream = File.OpenWrite(tempFile.FilePath);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

                await ApplicationYamlWriter.WriteAsync(output, writer, application);
            }

            var ns = $"namespace ${application.Namespace}";
            if (string.IsNullOrEmpty(application.Namespace))
            {
                ns = "current namespace";
            }
            output.WriteDebugLine($"Running 'kubectl apply' in ${ns}");
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

            output.WriteInfoLine($"Deployed application '{application.Name}'.");
        }
    }
}
