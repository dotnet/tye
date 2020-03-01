using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO;
using System.Linq;
using Micronetes.Hosting.Model;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tye
{
    static partial class Program
    {
        private static Command CreateInitCommand()
        {
            var command = new Command("init", "create a yaml manifest")
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
                if (File.Exists("tye.yaml"))
                {
                    console.Out.WriteLine("\"tye.yaml\" already exists.");
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
                    var application = ResolveApplication(path);
                    if (application is object)
                    {
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

                File.WriteAllText("tye.yaml", template);
                console.Out.WriteLine("Created \"tye.yaml\"");
            });

            return command;
        }

        private static Application? ResolveApplication(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = ResolveFileFromDirectory(Directory.GetCurrentDirectory());
            }
            else if (Directory.Exists(path))
            {
                path = ResolveFileFromDirectory(Path.GetFullPath(path));
            }

            if (path == null)
            {
                return null;
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

        private static string? ResolveFileFromDirectory(string basePath)
        {
            var formats = new[] { "tye.yaml", "tye.yml", "*.csproj", "*.fsproj", "*.sln" };

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

            return null;
        }
    }
}