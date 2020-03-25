// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            };

            command.AddOption(new Option(new[] { "-f", "--force" })
            {
                Description = "Override validation and force deployment.",
                Required = false
            });

            command.Handler = CommandHandler.Create<IConsole, FileInfo, Verbosity, bool, bool>(async (console, path, verbosity, interactive, force) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(console, verbosity);
                var application = await ApplicationFactory.CreateAsync(output, path);

                if (application.Services.Count == 0)
                {
                    throw new CommandException($"No services found in \"{application.Source.Name}\"");
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

            var steps = new List<ServiceExecutor.Step>()
            {
                new CombineStep() { Environment = environment, },
                new PublishProjectStep(),
                new BuildDockerImageStep() { Environment = environment, },
                new PushDockerImageStep() { Environment = environment, },
                new ValidateSecretStep() { Environment = environment, Interactive = interactive, Force = force, },
            };

            steps.Add(new GenerateKubernetesManifestStep() { Environment = environment, });
            steps.Add(new DeployServiceYamlStep() { Environment = environment, });

            ApplyRegistryAndDefaults(output, application, interactive, requireRegistry: true);

            var executor = new ServiceExecutor(output, application, steps);
            foreach (var service in application.Services)
            {
                await executor.ExecuteAsync(service);
            }

            await DeployApplicationManifestAsync(output, application, application.Source.Directory.Name, environment);
        }

        private static async Task DeployApplicationManifestAsync(OutputContext output, ApplicationBuilder application, string applicationName, string environment)
        {
            using var step = output.BeginStep("Deploying Application Manifests...");

            using var tempFile = TempFile.Create();
            output.WriteInfoLine($"Writing output to '{tempFile.FilePath}'.");

            {
                using var stream = File.OpenWrite(tempFile.FilePath);
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                await ApplicationYamlWriter.WriteAsync(output, writer, application);
            }

            output.WriteDebugLine("Running 'kubectl apply'.");
            output.WriteCommandLine("kubectl", $"apply -f \"{tempFile.FilePath}\"");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
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

            output.WriteInfoLine($"Deployed application '{applicationName}'.");

            step.MarkComplete();
        }

        internal static void ApplyRegistryAndDefaults(OutputContext output, ApplicationBuilder application, bool interactive, bool requireRegistry)
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

            foreach (var service in application.Services)
            {
                if (service is ProjectServiceBuilder project && project.ContainerInfo is ContainerInfo container)
                {
                    DockerfileGenerator.ApplyContainerDefaults(application, project, container);
                }
            }
        }
    }
}
