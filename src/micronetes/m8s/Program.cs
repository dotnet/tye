using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.CommandLine.Rendering;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Micronetes.Hosting;
using Micronetes.Hosting.Model;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Micronetes.Host
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var command = new RootCommand();

            command.Add(RunCommand(args));
            command.Add(NewCommand());

            command.Description = "Process manager and orchestrator for microservices.";

            var builder = new CommandLineBuilder(command);
            builder.UseHelp();
            builder.UseVersionOption();
            builder.UseDebugDirective();
            builder.UseParseErrorReporting();
            builder.ParseResponseFileAs(ResponseFileHandling.ParseArgsAsSpaceSeparated);
            builder.UsePrefixes(new[] { "-", "--", }); // disable garbage windows conventions

            builder.CancelOnProcessTermination();
            builder.UseExceptionHandler(HandleException);

            // Allow fancy drawing.
            builder.UseAnsiTerminalWhenAvailable();

            var parser = builder.Build();
            return await parser.InvokeAsync(args);
        }

        private static Command NewCommand()
        {
            var command = new Command("new", "create a yaml manifest")
            {
            };

            var argument = new Argument("path")
            {
                Description = "A solution or project file to generate a yaml manifest from",
                Arity = ArgumentArity.ZeroOrOne
            };

            command.AddArgument(argument);

            command.Handler = CommandHandler.Create<IConsole, string>((console, path) =>
            {
                if (File.Exists("m8s.yaml"))
                {
                    console.Out.WriteLine("\"m8s.yaml\" already exists.");
                    return;
                }

                var template = @"- name: app
  # project: app.csproj # msbuild project path (relative to this file)
  # executable: app.exe # path to an executable (relative to this file)
  # args: --arg1=3 # arguments to pass to the process
  # replicas: 5 # number of times to launch the application
  # env: # array of environment variables
  #  - name: key
  #    value: value
  # bindings: # optional array of bindings (ports, connection strings)
    # - port: 8080 # number port of the binding
";

                try
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        var application = ResolveApplication(path);
                        var serializer = new SerializerBuilder()
                            .WithNamingConvention(CamelCaseNamingConvention.Instance)
                            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                            .Build();

                        var extension = Path.GetExtension(application.Source).ToLowerInvariant();
                        var directory = Path.GetDirectoryName(application.Source);
                        var descriptions = application.Services.Select(s => s.Value.Description).ToList();

                        // Clear all bindings if any for solutions and project files
                        if (extension == ".sln" || extension == ".csproj" || extension == ".fsproj")
                        {
                            foreach (var d in descriptions)
                            {
                                d.Bindings = null;
                                d.Replicas = null;
                                d.Build = null;
                                d.Configuration = null;
                                d.Project = d.Project.Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar);
                            }
                        }

                        template = serializer.Serialize(descriptions);
                    }
                }
                catch (FileNotFoundException)
                {
                    // No file found, just generate a new one
                }

                File.WriteAllText("m8s.yaml", template);
                console.Out.WriteLine("Created \"m8s.yaml\"");
            });

            return command;
        }

        private static Command RunCommand(string[] args)
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
                Application app = ResolveApplication(path);
                return MicronetesHost.RunAsync(app, args);
            });

            return command;
        }

        private static Application ResolveApplication(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = ResolveFileFromDirectory(Directory.GetCurrentDirectory());
            }
            else if (Directory.Exists(path))
            {
                path = ResolveFileFromDirectory(Path.GetFullPath(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"{path} does not exist");
            }

            switch (Path.GetExtension(path).ToLower())
            {
                case ".yaml":
                case ".yml":
                    return Application.FromYaml(path);
                case ".csproj":
                case ".fsproj":
                    return Application.FromProject(path);
                case ".sln":
                    return Application.FromSolution(path);
                default:
                    throw new NotSupportedException($"{path} not supported");
            }
        }

        private static string ResolveFileFromDirectory(string basePath)
        {
            var formats = new[] { "m8s.yaml", "m8s.yml", "*.csproj", "*.fsproj", "*.sln" };

            foreach (var format in formats)
            {
                var files = Directory.GetFiles(basePath, format);
                if (files.Length == 0)
                {
                    continue;
                }

                if (files.Length > 1)
                {
                    throw new InvalidOperationException($"Ambiguous match found {string.Join(", ", files.Select(Path.GetFileName))}");
                }

                return files[0];
            }

            throw new InvalidOperationException($"None of the supported files were found (m8s.yaml, .csproj, .fsproj, .sln)");
        }

        private static void HandleException(Exception exception, InvocationContext context)
        {
            // context.Console.ResetTerminalForegroundColor();
            // context.Console.SetTerminalForegroundColor(ConsoleColor.Red);

            if (exception is OperationCanceledException)
            {
                context.Console.Error.WriteLine("operation canceled.");
            }
            else if (exception is TargetInvocationException tae)
            {
                context.Console.Error.WriteLine(tae.InnerException.Message);
            }
            else
            {
                context.Console.Error.WriteLine("unhandled exception: ");
                context.Console.Error.WriteLine(exception.ToString());
            }

            // context.Console.ResetTerminalForegroundColor();

            context.ResultCode = 1;
        }
    }
}
