// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Tye.ConfigModel;

namespace Microsoft.Tye
{
    static partial class Program
    {
        public static Command CreateDeployCommand()
        {
            var command = new Command("deploy", "deploy the application")
            {
                CommonArguments.Path_Required,
                StandardOptions.Interactive,
                StandardOptions.Verbosity,
                StandardOptions.Namespace,
            };

            command.AddOption(new Option(new[] { "-f", "--force" })
            {
                Description = "Override validation and force deployment.",
                Required = false
            });

            command.Handler = CommandHandler.Create<IConsole, FileInfo, Verbosity, bool, bool, string>(async (console, path, verbosity, interactive, force, @namespace) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(console, verbosity);

                output.WriteInfoLine("Loading Application Details...");
                var application = await ApplicationFactory.CreateAsync(output, path);
                if (application.Services.Count == 0)
                {
                    throw new CommandException($"No services found in \"{application.Source.Name}\"");
                }
                if (!String.IsNullOrEmpty(@namespace))
                {
                    application.Namespace = @namespace;
                }
                await ExecuteDeployAsync(new OutputContext(console, verbosity), application, environment: "production", interactive, force);
            });

            return command;
        }

        private static async Task ExecuteDeployAsync(OutputContext output, ApplicationBuilder application, string environment, bool interactive, bool force)
        {
            if (!await KubectlDetector.Instance.IsKubectlInstalled.Value)
            {
                throw new CommandException($"Cannot apply manifests because kubectl is not installed.");
            }

            if (!await KubectlDetector.Instance.IsKubectlConnectedToCluster.Value)
            {
                throw new CommandException($"Cannot apply manifests because kubectl is not connected to a cluster.");
            }

            await application.ProcessExtensionsAsync(output, ExtensionContext.OperationKind.Deploy);
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
                    new ValidateSecretStep() { Environment = environment, Interactive = interactive, Force = force, },
                    new GenerateServiceKubernetesManifestStep() { Environment = environment, },
                },

                IngressSteps =
                {
                    new GenerateIngressKubernetesManifestStep(),
                },

                ApplicationSteps =
                {
                    new DeployApplicationKubernetesManifestStep(),
                }
            };

            await executor.ExecuteAsync(application);
        }

        internal static void ApplyRegistry(OutputContext output, ApplicationBuilder application, bool interactive, bool requireRegistry)
        {
            if (application.Registry is null && interactive)
            {
                var registry = output.Prompt("Enter the Container Registry (ex: 'example.azurecr.io' for Azure or 'example' for dockerhub)", allowEmpty: !requireRegistry);
                if (!string.IsNullOrWhiteSpace(registry))
                {
                    application.Registry = new ContainerRegistry(registry.Trim());
                }
            }
            else if (application.Registry is null && requireRegistry)
            {
                throw new CommandException("A registry is required for deploy operations. Add the registry to 'tye.yaml' or use '-i' for interactive mode.");
            }
            else
            {
                // No registry specified, and that's OK!
            }
        }
    }
}
