// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    static partial class Program
    {
        public static Command CreatePushCommand()
        {
            var command = new Command("push", "build and push application containers to registry")
            {
                CommonArguments.Path_Required,
                StandardOptions.Interactive,
                StandardOptions.Verbosity,
                StandardOptions.Tags
            };

            command.AddOption(new Option(new[] { "-f", "--force" })
            {
                Description = "Override validation and force push.",
                Required = false
            });

            command.Handler = CommandHandler.Create<IConsole, FileInfo, Verbosity, bool, bool, string[]>(async (console, path, verbosity, interactive, force, tags) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(console, verbosity);

                output.WriteInfoLine("Loading Application Details...");
                var filter = ApplicationFactoryFilter.GetApplicationFactoryFilter(tags);

                var application = await ApplicationFactory.CreateAsync(output, path, null, filter);
                if (application.Services.Count == 0)
                {
                    throw new CommandException($"No services found in \"{application.Source.Name}\"");
                }

                await ExecutePushAsync(new OutputContext(console, verbosity), application, environment: "production", interactive, force);
            });

            return command;
        }

        private static async Task ExecutePushAsync(OutputContext output, ApplicationBuilder application, string environment, bool interactive, bool force)
        {
            await application.ProcessExtensionsAsync(options: null, output, ExtensionContext.OperationKind.Deploy);
            ApplyRegistry(output, application, interactive, requireRegistry: true);

            var executor = new ApplicationExecutor(output)
            {
                ServiceSteps =
                {
                    new ApplyContainerDefaultsStep(),
                    new CombineStep() { Environment = environment, },
                    new PublishProjectStep(),
                    new BuildDockerImageStep() { Environment = environment, },
                    new PushDockerImageStep() { Environment = environment, },
                },
            };

            await executor.ExecuteAsync(application);
        }
    }
}
