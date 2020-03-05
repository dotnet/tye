// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace Tye
{
    internal static class HelmPush
    {
        public static async Task ExecuteAsync(OutputContext output, string registryHostname, string chartFilePath)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (registryHostname is null)
            {
                throw new ArgumentNullException(nameof(registryHostname));
            }

            if (chartFilePath is null)
            {
                throw new ArgumentNullException(nameof(chartFilePath));
            }

            // NOTE: this is currently hardcoded to use ACR and uses the azure CLI to do
            // the push operation. Helm 3 has support for pushing to OCI repositories,
            // but it's not documented that it works yet for azure.
            if (!registryHostname.EndsWith(".azurecr.io"))
            {
                throw new CommandException("Currently pushing of helm charts is limited to '*.azurecr.io'.");
            }

            var registryName = registryHostname.Substring(0, registryHostname.Length - ".azurecr.io".Length);

            output.WriteDebugLine("Running 'az acr helm push'.");
            output.WriteCommandLine("az", $"acr helm push --name {registryName} \"{chartFilePath}\"");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
                $"az",
                $"acr helm push --name {registryName} \"{chartFilePath}\"",
                stdOut: capture.StdOut,
                stdErr: capture.StdErr);

            output.WriteDebugLine($"Done running 'az acr helm push' exit code: {exitCode}");
            if (exitCode != 0)
            {
                throw new CommandException("'az acr helm push' failed.");
            }
        }
    }
}
