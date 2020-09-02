// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
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
                StandardOptions.Framework,
                StandardOptions.Tags,
                StandardOptions.CreateForce("Override validation and force deployment.")
            };

            command.Handler = CommandHandler.Create<DeployCommandArguments>(async args =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (args.Path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(args.Console, args.Verbosity);
                output.WriteInfoLine("Loading Application Details...");

                var filter = ApplicationFactoryFilter.GetApplicationFactoryFilter(args.Tags);

                var application = await ApplicationFactory.CreateAsync(output, args.Path, args.Framework, filter);
                if (application.Services.Count == 0)
                {
                    throw new CommandException($"No services found in \"{application.Source.Name}\"");
                }

                if (!string.IsNullOrEmpty(args.Namespace))
                {
                    application.Namespace = args.Namespace;
                }

                var executeOutput = new OutputContext(args.Console, args.Verbosity);
                await ExecuteDeployAsync(executeOutput, application, environment: "production", args.Interactive, args.Force);
            });

            return command;
        }

        private static async Task ExecuteDeployAsync(OutputContext output, ApplicationBuilder application, string environment, bool interactive, bool force)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (!await KubectlDetector.IsKubectlInstalledAsync(output))
            {
                throw new CommandException($"Cannot apply manifests because kubectl is not installed.");
            }

            if (!await KubectlDetector.IsKubectlConnectedToClusterAsync(output))
            {
                throw new CommandException($"Cannot apply manifests because kubectl is not connected to a cluster.");
            }

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
                    new ValidateSecretStep() { Environment = environment, Interactive = interactive, Force = force, },
                    new GenerateServiceKubernetesManifestStep() { Environment = environment, },
                },

                IngressSteps =
                {
                    new ValidateIngressStep() { Environment = environment, Interactive = interactive, Force = force, },
                    new GenerateIngressKubernetesManifestStep(),
                },

                ApplicationSteps =
                {
                    new DeployApplicationKubernetesManifestStep(),
                }
            };

            await executor.ExecuteAsync(application);

            watch.Stop();

            TimeSpan elapsedTime = watch.Elapsed;

            output.WriteAlwaysLine($"Time Elapsed: {elapsedTime.Hours:00}:{elapsedTime.Minutes:00}:{elapsedTime.Seconds:00}:{elapsedTime.Milliseconds / 10:00}");
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

        private class DeployCommandArguments
        {
            public IConsole Console { get; set; } = default!;

            public FileInfo Path { get; set; } = default!;

            public Verbosity Verbosity { get; set; }

            public string Namespace { get; set; } = default!;

            public bool Interactive { get; set; } = false;

            public string Framework { get; set; } = default!;

            public bool Force { get; set; } = false;

            public string[] Tags { get; set; } = Array.Empty<string>();
        }
    }
}
