// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    static partial class Program
    {
        public static Command CreateBuildPushDeployCommand()
        {
            var command = new Command("build-push-deploy", "build, push, and deploy the application")
            {
                CommonArguments.Path_Required,
                StandardOptions.Interactive,
                StandardOptions.Verbosity,
                StandardOptions.Namespace,
                StandardOptions.Framework,
                StandardOptions.Tags,
                StandardOptions.Environment,
                StandardOptions.IncludeLatestTag,
                StandardOptions.Debug,
                StandardOptions.CreateForce("Override validation and force deployment.")
            };

            command.Handler = CommandHandler.Create<BuildPushDeployCommandArguments>(async args =>
            {
                if (args.Debug)
                {
                    Console.WriteLine("Debug mode is on. Waiting for debugger to attach");
                    while (!Debugger.IsAttached)
                        await Task.Delay(100);
                    Console.WriteLine("Debugger attached");
                }

                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (args.Path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(args.Console, args.Verbosity);
                output.WriteInfoLine("Loading Application Details...");

                var filter = ApplicationFactoryFilter.GetApplicationFactoryFilter(args.Tags);

                var application = await ApplicationFactory.CreateAsync(output, args.Path, args.Framework, filter, args.Environment);
                if (application.Services.Count == 0)
                {
                    throw new CommandException($"No services found in \"{application.Source.Name}\"");
                }

                if (!string.IsNullOrEmpty(args.Namespace))
                {
                    application.Namespace = args.Namespace;
                }

                var executeOutput = new OutputContext(args.Console, args.Verbosity);
                await ExecuteBuildPushDeployAsync(executeOutput, application, environment: args.Environment, args.Interactive, args.Force, args.IncludeLatestTag);
            });

            return command;
        }

        private static async Task ExecuteBuildPushDeployAsync(OutputContext output, ApplicationBuilder application, string environment, bool interactive, bool force, bool includeLatestTag)
        {
            var watch = Stopwatch.StartNew();

            if (await KubectlDetector.GetKubernetesServerVersion(output) == null)
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
                    new PushDockerImageStep() { Environment = environment, IncludeLatestTag = includeLatestTag, },
                    new ValidateSecretStep() { Environment = environment, Interactive = interactive, Force = force, },
                    new GenerateServiceKubernetesManifestStep() { Environment = environment, },
                },

                IngressSteps =
                {
                    new ValidateIngressStep() { Environment = environment, Interactive = interactive, Force = force, },
                    new GenerateIngressKubernetesManifestStep { Environment = environment, },
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

        private class BuildPushDeployCommandArguments
        {
            public IConsole Console { get; set; } = default!;

            public FileInfo Path { get; set; } = default!;

            public Verbosity Verbosity { get; set; }

            public string Namespace { get; set; } = default!;

            public bool Interactive { get; set; } = false;

            public string Framework { get; set; } = default!;

            public bool Force { get; set; } = false;

            public string[] Tags { get; set; } = Array.Empty<string>();

            public string Environment { get; set; } = default!;

            public bool IncludeLatestTag { get; set; } = true;

            public bool Debug { get; set; } = false;
        }
    }
}
