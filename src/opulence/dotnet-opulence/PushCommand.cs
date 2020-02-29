using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace Opulence
{
    public class PushCommand
    {
        public static Command Create()
        {
            var command = new Command("push", "Push the application to registry")
            {
                StandardOptions.Project,
                StandardOptions.Verbosity,
                StandardOptions.Environment,
            };

            command.Handler = CommandHandler.Create<IConsole, FileInfo, string, Verbosity>((console, project, environment, verbosity) =>
            {
                return ExecuteAsync(new OutputContext(console, verbosity), project, environment);
            });

            return command;
        }

        private static async Task ExecuteAsync(OutputContext output, FileInfo projectFile, string environment)
        {
            output.WriteBanner();

            var application = await ApplicationFactory.CreateApplicationAsync(output, projectFile);
            if (application.Globals.Registry?.Hostname == null)
            {
                throw new CommandException("A registry is required for push operations. run 'dotnet-opulence init'.");
            }

            var steps = new ServiceExecutor.Step[]
            {
                new CombineStep() { Environment = environment, },
                new BuildDockerImageStep() { Environment = environment, },
                new PushDockerImageStep() { Environment = environment, },
            };

            var executor = new ServiceExecutor(output, application, steps);
            foreach (var service in application.Services)
            {
                if (service.IsMatchForProject(application, projectFile))
                {
                    await executor.ExecuteAsync(service);
                }
            }
        }
    }
}
