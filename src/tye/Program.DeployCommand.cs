using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Opulence;

namespace Tye
{
    static partial class Program
    {
        public static Command CreateDeployCommand()
        {
            var command = new Command("deploy", "Deploy the application");

            var argument = new Argument("path")
            {
                Description = "A solution or project file to generate a yaml manifest from",
                Arity = ArgumentArity.ZeroOrOne
            };

            command.Add(argument);
            command.Add(StandardOptions.Interactive);
            command.Add(StandardOptions.Verbosity);

            command.Handler = CommandHandler.Create<IConsole, string, Verbosity, bool>((console, path, verbosity, interactive) =>
            {
                var application = ResolveApplication(path);
                if (application is null)
                {
                    throw new CommandException($"None of the supported files were found (tye.yaml, .csproj, .fsproj, .sln)");
                }

                return ExecuteAsync(new OutputContext(console, verbosity), application, environment: "production", interactive);
            });

            return command;
        }

        private static async Task ExecuteAsync(OutputContext output, Micronetes.Hosting.Model.Application application, string environment, bool interactive)
        {
            var globals = new ApplicationGlobals();
            var services = new List<Opulence.ServiceEntry>();

            foreach (var kvp in application.Services)
            {
                if (kvp.Value.Description.Project is string projectFile)
                {
                    var project = new Project(projectFile);
                    var service = new Service(kvp.Key)
                    {
                        Source = project,
                    };
                    var serviceEntry = new ServiceEntry(service, kvp.Key);

                    await ProjectReader.ReadProjectDetailsAsync(output, new FileInfo(projectFile), project);

                    var container = new ContainerInfo();
                    service.GeneratedAssets.Container = container;
                    services.Add(serviceEntry);
                }
            }

            var opulenceApplication = new OpulenceApplicationAdapter(application, globals, services);
            if (opulenceApplication.Globals.Registry?.Hostname == null && interactive)
            {
                var registry = output.Prompt("Enter the Container Registry (ex: 'example.azurecr.io' for Azure or 'example' for dockerhub)");
                opulenceApplication.Globals.Registry = new ContainerRegistry(registry);
            }
            else if (opulenceApplication.Globals.Registry?.Hostname == null)
            {
                throw new CommandException("A registry is required for deploy operations. Add the registry to 'tye.yaml' or use '-i' for interactive mode.");
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
                new BuildDockerImageStep() { Environment = environment, },
                new PushDockerImageStep() { Environment = environment, },
            };

            steps.Add(new GenerateKubernetesManifestStep() { Environment = environment, });

            // If this is command is for a project, then deploy the component manifest
            // for just the project. We won't run the "application deploy" part.
            if (!string.Equals(".csproj", Path.GetExtension(application.Source), StringComparison.Ordinal) &&
                !string.Equals(".fsproj", Path.GetExtension(application.Source), StringComparison.Ordinal))
            {
                steps.Add(new DeployServiceYamlStep() { Environment = environment, });
            }

            var executor = new ServiceExecutor(output, opulenceApplication, steps);
            foreach (var service in opulenceApplication.Services)
            {
                if (service.IsMatchForProject(opulenceApplication, new FileInfo(application.Source)))
                {
                    await executor.ExecuteAsync(service);
                }
            }

            await PackageApplicationAsync(output, opulenceApplication, Path.GetDirectoryName(application.Source), environment);
        }

        private static async Task PackageApplicationAsync(OutputContext output, Opulence.Application application, string applicationName, string environment)
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
    }
}
