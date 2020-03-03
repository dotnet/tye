using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using Tye.Hosting;
using Tye.ConfigModel;

namespace Tye
{
    static partial class Program
    {
        private static Command CreateRunCommand(string[] args)
        {
            var command = new Command("run", "run the application")
            {
                CommonArguments.Path_Required,
            };

            // TODO: We'll need to support a --build-args
            command.AddOption(new Option("--no-build")
            {
                Description = "Do not build project files before running.",
                Required = false
            });

            command.AddOption(new Option("--port")
            {
                Description = "The port to run control plane on.",
                Argument = new Argument<int>("port"),
                Required = false
            });

            command.AddOption(new Option("--logs")
            {
                Description = "Write structured application logs to the specified log providers. Supported providers are console, elastic (Elasticsearch), ai (ApplicationInsights), seq.",
                Argument = new Argument<string>("logs"),
                Required = false
            });

            command.AddOption(new Option("--dtrace")
            {
                Description = "Write distributed traces to the specified providers. Supported providers are zipkin.",
                Argument = new Argument<string>("logs"),
                Required = false
            });

            command.AddOption(new Option("--debug")
            {
                Description = "Wait for debugger attach in all services.",
                Required = false
            });

            command.Handler = CommandHandler.Create<IConsole, FileInfo>((console, path) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var application = ConfigFactory.FromFile(path);
                return TyeHost.RunAsync(application.ToHostingApplication(), args);
            });

            return command;
        }
    }
}
