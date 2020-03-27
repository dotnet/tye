// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public static class GenerateHost
    {
        public static async Task GenerateAsync(IConsole console, FileInfo path, Verbosity verbosity, bool interactive, CancellationToken cancellationToken = default)
        {
            var output = new OutputContext(console, verbosity);
            var application = await ApplicationFactory.CreateAsync(output, path, cancellationToken);
            await ExecuteGenerateAsync(output, application, environment: "production", interactive, cancellationToken);
        }

        public static async Task ExecuteGenerateAsync(OutputContext output, ApplicationBuilder application, string environment, bool interactive, CancellationToken cancellationToken = default)
        {
            var steps = new List<ServiceExecutor.Step>()
            {
                new CombineStep() { Environment = environment, },
                new PublishProjectStep(),
                new BuildDockerImageStep() { Environment = environment, }, // Make an image but don't push it
            };

            steps.Add(new GenerateKubernetesManifestStep() { Environment = environment, });

            Program.ApplyRegistryAndDefaults(output, application, interactive, requireRegistry: false);

            var executor = new ServiceExecutor(output, application, steps);
            foreach (var service in application.Services)
            {
                await executor.ExecuteAsync(service, cancellationToken);
            }

            await GenerateApplicationManifestAsync(output, application, environment, cancellationToken);
        }

        private static async Task GenerateApplicationManifestAsync(OutputContext output, ApplicationBuilder application, string environment, CancellationToken cancellationToken)
        {
            using var step = output.BeginStep("Generating Application Manifests...");

            var outputFilePath = Path.GetFullPath(Path.Combine(application.Source.DirectoryName, $"{application.Name}-generate-{environment}.yaml"));
            output.WriteInfoLine($"Writing output to '{outputFilePath}'.");
            {
                File.Delete(outputFilePath);

                await using var stream = File.OpenWrite(outputFilePath);
                await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                await ApplicationYamlWriter.WriteAsync(output, writer, application, cancellationToken);
            }

            step.MarkComplete();
        }
    }
}
