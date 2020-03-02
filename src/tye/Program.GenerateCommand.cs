using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Opulence;
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
                StandardOptions.Verbosity,
            };

            // This is a super-secret VIP-only command! It's useful for testing, but we're 
            // not documenting it right now.
            command.IsHidden = true;

            command.Handler = CommandHandler.Create<IConsole, FileInfo, Verbosity>((console, path, verbosity) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var application = ConfigFactory.FromFile(path);
                return ExecuteAsync(new OutputContext(console, verbosity), application, environment: "production");
            });

            return command;
        }

        private static async Task ExecuteAsync(OutputContext output, ConfigApplication application, string environment)
        {
            var globals = new ApplicationGlobals()
            {
                Name = application.Name,
                Registry = application.Registry is null ? null : new ContainerRegistry(application.Registry),
            };

            var services = new List<Opulence.ServiceEntry>();
            foreach (var configService in application.Services)
            {
                if (configService.Project is string projectFile)
                {
                    var project = new Project(projectFile);
                    var service = new Service(configService.Name)
                    {
                        Source = project,
                    };
                    var serviceEntry = new ServiceEntry(service, configService.Name);

                    await ProjectReader.ReadProjectDetailsAsync(output, new FileInfo(projectFile), project);

                    var container = new ContainerInfo();
                    service.GeneratedAssets.Container = container;
                    services.Add(serviceEntry);
                }
            }

            var opulenceApplication = new OpulenceApplicationAdapter(application, globals, services);
            if (opulenceApplication.Globals.Registry?.Hostname == null )
            {
                var registry = output.Prompt("Enter the Container Registry (ex: 'example.azurecr.io' for Azure or 'example' for dockerhub)");
                opulenceApplication.Globals.Registry = new ContainerRegistry(registry);
            }
            else if (opulenceApplication.Globals.Registry?.Hostname == null)
            {
                throw new CommandException("A registry is required for generate operations. Add the registry to 'tye.yaml' or use '-i' for interactive mode.");
            }

            foreach (var service in opulenceApplication.Services)
            {
                if (service.Service.Source is Project project && service.Service.GeneratedAssets.Container is ContainerInfo container)
                {
                    DockerfileGenerator.ApplyContainerDefaults(opulenceApplication, service, project, container);
                }
            }

            var steps = new List<ServiceExecutor.Step>()
            {
                new CombineStep() { Environment = environment, },
                new BuildDockerImageStep() { Environment = environment, }, // Make an image but don't push it
            };

            steps.Add(new GenerateKubernetesManifestStep() { Environment = environment, });

            var executor = new ServiceExecutor(output, opulenceApplication, steps);
            foreach (var service in opulenceApplication.Services)
            {
                await executor.ExecuteAsync(service);
            }

            await GenerateApplicationManifestAsync(output, opulenceApplication, application.Source.Directory.Name, environment);
        }

        private static async Task GenerateApplicationManifestAsync(OutputContext output, Opulence.Application application, string applicationName, string environment)
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
