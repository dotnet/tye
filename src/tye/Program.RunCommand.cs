using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using Micronetes.Hosting;

namespace Tye
{
    static partial class Program
    {
        private static Command CreateRunCommand(string[] args)
        {
            var command = new Command("run", "run the application")
            {
            };

            var argument = new Argument("path")
            {
                Description = "A file or directory to execute. Supports a project files, solution files or a yaml manifest.",
                Arity = ArgumentArity.ZeroOrOne
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

            command.AddArgument(argument);

            command.Handler = CommandHandler.Create<IConsole, string>((console, path) =>
            {
                var application = ResolveApplication(path);
                if (application is null)
                {
                    throw new CommandException($"None of the supported files were found (tye.yaml, .csproj, .fsproj, .sln)");
                }
                
                return MicronetesHost.RunAsync(application, args);
            });

            return command;
        }
    }
}