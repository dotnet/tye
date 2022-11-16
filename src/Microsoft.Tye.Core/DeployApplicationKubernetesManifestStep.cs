// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
            using var step = output.BeginStep("Applying Kubernetes Manifests...");

            if (await KubectlDetector.GetKubernetesServerVersion(output) == null)
            {
                throw new CommandException($"Cannot apply manifests because kubectl is not installed.");
            }

            if (!await KubectlDetector.IsKubectlConnectedToClusterAsync(output))
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
            var exitCode = await ProcessUtil.ExecuteAsync(
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
            if (application.Ingress.Count > 0)
            {
                output.WriteInfoLine($"Waiting for ingress to be deployed. This may take a long time.");
                foreach (var ingress in application.Ingress)
                {
                    using var ingressStep = output.BeginStep($"Retrieving details for {ingress.Name}...");

                    var done = false;

                    Action<string> complete = line =>
                    {
                        done = line != "''";
                        if (done)
                        {
                            output.WriteInfoLine($"IngressIP: {line}");
                        }
                    };

                    var retries = 0;
                    var namespaceArg = string.IsNullOrEmpty(application.Namespace) ? string.Empty : $"-n {application.Namespace}";
                    while (!done && retries < 60)
                    {
                        var ingressExitCode = await ProcessUtil.ExecuteAsync(
                            "kubectl",
                            $"get ingress {ingress.Name} {namespaceArg} -o jsonpath='{{..ip}}'",
                            Environment.CurrentDirectory,
                            complete,
                            capture.StdErr);

                        if (ingressExitCode != 0)
                        {
                            throw new CommandException("'kubectl get ingress' failed");
                        }

                        if (!done)
                        {
                            await Task.Delay(2000);
                            retries++;
                        }
                    }
                }
            }
        }
    }
}
