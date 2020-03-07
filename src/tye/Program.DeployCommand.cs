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
        public static Command CreateDeployCommand()
        {
            var command = new Command("deploy", "Deploy the application")
            {
                CommonArguments.Path_Required,
                StandardOptions.Interactive,
                StandardOptions.Verbosity,
            };

            command.Handler = CommandHandler.Create<IConsole, FileInfo, Verbosity, bool>((console, path, verbosity, interactive) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var application = ConfigFactory.FromFile(path);
                return ExecuteDeployAsync(new OutputContext(console, verbosity), application, environment: "production", interactive);
            });

            return command;
        }

        private static async Task ExecuteDeployAsync(OutputContext output, ConfigApplication application, string environment, bool interactive)
        {
            var temporaryApplication = await CreateApplicationAdapterAsync(output, application, interactive);
            var steps = new List<ServiceExecutor.Step>()
            {
                new CombineStep() { Environment = environment, },
                new PublishProjectStep(),
                new BuildDockerImageStep() { Environment = environment, },
                new PushDockerImageStep() { Environment = environment, },
            };

            steps.Add(new GenerateKubernetesManifestStep() { Environment = environment, });
            steps.Add(new DeployServiceYamlStep() { Environment = environment, });

            var executor = new ServiceExecutor(output, temporaryApplication, steps);
            foreach (var service in temporaryApplication.Services)
            {
                await executor.ExecuteAsync(service);
            }

            await DeployApplicationManifestAsync(output, temporaryApplication, application.Source.Directory.Name, environment);
        }

        private static async Task<TemporaryApplicationAdapter> CreateApplicationAdapterAsync(OutputContext output, ConfigApplication application, bool interactive)
        {
            var globals = new ApplicationGlobals()
            {
                Name = application.Name,
                Registry = application.Registry is null ? null : new ContainerRegistry(application.Registry),
            };

            var services = new List<Tye.ServiceEntry>();
            foreach (var configService in application.Services)
            {
                if (configService.Project is string projectFile)
                {
                    var fullPathProjectFile = Path.Combine(application.Source.DirectoryName, projectFile);
                    var project = new Project(fullPathProjectFile);
                    var service = new Service(configService.Name)
                    {
                        Source = project,
                    };

                    service.Replicas = configService.Replicas ?? 1;

                    foreach (var configBinding in configService.Bindings)
                    {
                        service.Bindings.Add(new ServiceBinding(configBinding.Name ?? service.Name)
                        {
                            ConnectionString = configBinding.ConnectionString,
                            Host = configBinding.Host,
                            Port = configBinding.Port,
                            Protocol = configBinding.Protocol,
                        });
                    }

                    var serviceEntry = new ServiceEntry(service, configService.Name);

                    await ProjectReader.ReadProjectDetailsAsync(output, new FileInfo(fullPathProjectFile), project);

                    var container = new ContainerInfo()
                    {
                        UseMultiphaseDockerfile = false,
                    };
                    service.GeneratedAssets.Container = container;
                    services.Add(serviceEntry);
                }
                else
                {
                    // For a non-project, we don't really need much info about it, just the name and bindings
                    var service = new Service(configService.Name);
                    foreach (var configBinding in configService.Bindings)
                    {
                        service.Bindings.Add(new ServiceBinding(configBinding.Name ?? service.Name)
                        {
                            ConnectionString = configBinding.ConnectionString,
                            Host = configBinding.Host,
                            Port = configBinding.Port,
                            Protocol = configBinding.Protocol,
                        });
                    }

                    var serviceEntry = new ServiceEntry(service, configService.Name);
                    services.Add(serviceEntry);
                }
            }

            var temporaryApplication = new TemporaryApplicationAdapter(application, globals, services);
            if (temporaryApplication.Globals.Registry?.Hostname == null && interactive)
            {
                var registry = output.Prompt("Enter the Container Registry (ex: 'example.azurecr.io' for Azure or 'example' for dockerhub)");
                temporaryApplication.Globals.Registry = new ContainerRegistry(registry);
            }
            else if (temporaryApplication.Globals.Registry?.Hostname == null)
            {
                throw new CommandException("A registry is required for deploy operations. Add the registry to 'tye.yaml' or use '-i' for interactive mode.");
            }

            foreach (var service in temporaryApplication.Services)
            {
                if (service.Service.Source is Project project && service.Service.GeneratedAssets.Container is ContainerInfo container)
                {
                    DockerfileGenerator.ApplyContainerDefaults(temporaryApplication, service, project, container);
                }
            }

            return temporaryApplication;
        }

        private static async Task DeployApplicationManifestAsync(OutputContext output, Tye.Application application, string applicationName, string environment)
        {
            using var step = output.BeginStep("Deploying Application Manifests...");

            using var tempFile = TempFile.Create();
            output.WriteInfoLine($"Writing output to '{tempFile.FilePath}'.");

            {
                using var stream = File.OpenWrite(tempFile.FilePath);
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                await ApplicationYamlWriter.WriteAsync(output, writer, application);
            }

            if (!await KubectlDetector.Instance.IsKubectlInstalled.Value)
            {
                throw new CommandException($"Cannot apply manifests because kubectl is not installed.");
            }

            if (!await KubectlDetector.Instance.IsKubectlConnectedToCluster.Value)
            {
                throw new CommandException($"Cannot apply manifests because kubectl is not connected to a cluster.");
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
    }
}
