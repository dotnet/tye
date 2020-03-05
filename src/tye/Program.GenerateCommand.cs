// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tye.ConfigModel;

namespace Tye
{
    static partial class Program
    {
        public static Command CreateGenerateCommand()
        {
            var command = new Command("generate", "Generate kubernetes manifests")
            {
                CommonArguments.Path_Required,
                StandardOptions.Interactive,
                StandardOptions.Verbosity,
            };

            // This is a super-secret VIP-only command! It's useful for testing, but we're 
            // not documenting it right now.
            command.IsHidden = true;

            command.Handler = CommandHandler.Create<IConsole, FileInfo, Verbosity, bool>((console, path, verbosity, interactive) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var application = ConfigFactory.FromFile(path);
                return ExecuteGenerateAsync(new OutputContext(console, verbosity), application, environment: "production", interactive);
            });

            return command;
        }

        private static async Task ExecuteGenerateAsync(OutputContext output, ConfigApplication application, string environment, bool interactive)
        {
            var temporaryApplication = await CreateApplicationAdapterAsync(output, application, interactive);
            var steps = new List<ServiceExecutor.Step>()
            {
                new CombineStep() { Environment = environment, },
                new BuildDockerImageStep() { Environment = environment, }, // Make an image but don't push it
            };

            steps.Add(new GenerateKubernetesManifestStep() { Environment = environment, });

            var executor = new ServiceExecutor(output, temporaryApplication, steps);
            foreach (var service in temporaryApplication.Services)
            {
                await executor.ExecuteAsync(service);
            }

            await GenerateApplicationManifestAsync(output, temporaryApplication, application.Source.Directory.Name, environment);
        }

        private static async Task GenerateApplicationManifestAsync(OutputContext output, Tye.Application application, string applicationName, string environment)
        {
            using var step = output.BeginStep("Generating Application Manifests...");

            var outputFilePath = Path.GetFullPath(Path.Combine(".", $"{applicationName}-{environment}.yaml"));
            output.WriteInfoLine($"Writing output to '{outputFilePath}'.");

            {
                using var stream = File.OpenWrite(outputFilePath);
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                await ApplicationYamlWriter.WriteAsync(output, writer, application);
            }

            step.MarkComplete();
        }
    }
}
