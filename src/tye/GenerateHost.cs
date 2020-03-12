// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tye.ConfigModel;

namespace Tye
{
    public static class GenerateHost
    {
        public static Task GenerateAsync(IConsole console, FileInfo path, Verbosity verbosity, bool interactive)
        {
            var application = ConfigFactory.FromFile(path);
            return ExecuteGenerateAsync(new OutputContext(console, verbosity), application, environment: "production", interactive);
        }

        public static async Task ExecuteGenerateAsync(OutputContext output, ConfigApplication application, string environment, bool interactive)
        {
            var temporaryApplication = await Program.CreateApplicationAdapterAsync(output, application, interactive, requireRegistry: false);
            var steps = new List<ServiceExecutor.Step>()
            {
                new CombineStep() { Environment = environment, },
                new PublishProjectStep(),
                new BuildDockerImageStep() { Environment = environment, }, // Make an image but don't push it
            };

            steps.Add(new GenerateKubernetesManifestStep() { Environment = environment, });

            var executor = new ServiceExecutor(output, temporaryApplication, steps);
            foreach (var service in temporaryApplication.Services)
            {
                await executor.ExecuteAsync(service);
            }

            await GenerateApplicationManifestAsync(output, temporaryApplication, application.Name ?? application.Source.Directory.Name, environment);
        }

        private static async Task GenerateApplicationManifestAsync(OutputContext output, Tye.Application application, string applicationName, string environment)
        {
            using var step = output.BeginStep("Generating Application Manifests...");

            var outputFilePath = Path.GetFullPath(Path.Combine(application.RootDirectory, $"{applicationName}-generate-{environment}.yaml"));
            output.WriteInfoLine($"Writing output to '{outputFilePath}'.");
            {
                File.Delete(outputFilePath);

                using var stream = File.OpenWrite(outputFilePath);
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                await ApplicationYamlWriter.WriteAsync(output, writer, application);
            }

            step.MarkComplete();
        }
    }
}
