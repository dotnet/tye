// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public static class GenerateHost
    {
        public static async Task GenerateAsync(IConsole console, FileInfo path, Verbosity verbosity, bool interactive, string ns)
        {
            var output = new OutputContext(console, verbosity);

            output.WriteInfoLine("Loading Application Details...");
            var application = await ApplicationFactory.CreateAsync(output, path);
            if (application.Services.Count == 0)
            {
                throw new CommandException($"No services found in \"{application.Source.Name}\"");
            }
            if (!String.IsNullOrEmpty(ns))
            {
                application.Namespace = ns;
            }
            await ExecuteGenerateAsync(output, application, environment: "production", interactive);
        }

        public static async Task ExecuteGenerateAsync(OutputContext output, ApplicationBuilder application, string environment, bool interactive)
        {
            await application.ProcessExtensionsAsync(output, ExtensionContext.OperationKind.Deploy);
            Program.ApplyRegistry(output, application, interactive, requireRegistry: false);

            var executor = new ApplicationExecutor(output)
            {
                ServiceSteps =
                {
                    new ApplyContainerDefaultsStep(),
                    new CombineStep() { Environment = environment, },
                    new PublishProjectStep(),
                    new BuildDockerImageStep() { Environment = environment, }, // Make an image but don't push it
                    new GenerateKubernetesManifestStep() { Environment = environment, },
                },
            };
            await executor.ExecuteAsync(application);

            await GenerateApplicationManifestAsync(output, application, environment);
        }

        private static async Task GenerateApplicationManifestAsync(OutputContext output, ApplicationBuilder application, string environment)
        {
            using var step = output.BeginStep("Generating Application Manifests...");

            var outputFilePath = Path.GetFullPath(Path.Combine(application.Source.DirectoryName, $"{application.Name}-generate-{environment}.yaml"));
            output.WriteInfoLine($"Writing output to '{outputFilePath}'.");
            {
                File.Delete(outputFilePath);

                await using var stream = File.OpenWrite(outputFilePath);
                await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                await ApplicationYamlWriter.WriteAsync(output, writer, application);
            }

            step.MarkComplete();
        }
    }
}
