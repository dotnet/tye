using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace Opulence
{
    internal static class GenerateCommand
    {
        public static Command Create()
        {
            var command = new Command("generate", "Generate assets")
            {
                StandardOptions.Project,
                StandardOptions.Verbosity,
                StandardOptions.Outputs,
                StandardOptions.Force,
            };

            command.Handler = CommandHandler.Create<IConsole, FileInfo, Verbosity, List<string>, bool>((console, project, verbosity, outputs, force) =>
            {
                var output = new OutputContext(console, verbosity);
                return ExecuteAsync(output, project, outputs, force);
            });

            return command;
        }

        private static async Task ExecuteAsync(OutputContext output, FileInfo projectFile, List<string> outputs, bool force)
        {
            output.WriteBanner();

            var application = await ApplicationFactory.CreateApplicationAsync(output, projectFile);

            var steps = new List<ServiceExecutor.Step>();

            if (outputs.Count == 0 || outputs.Contains("container"))
            {
                steps.Add(new GenerateDockerfileStep(){ Force = force, });
            }

            if (outputs.Count == 0 || outputs.Contains("chart"))
            {
                steps.Add(new GenerateHelmChartStep());
            }

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