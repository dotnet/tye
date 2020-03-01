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
                CommonArguments.Path_Optional,
            };

            command.Handler = CommandHandler.Create<IConsole, FileInfo?>((console, path) =>
            {
                if (path is FileInfo &&
                    path.Exists &&
                    (string.Equals(".yml", path.Extension, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(".yaml", path.Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new CommandException($"File '{path.FullName}' already exists.");
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

                if (path is FileInfo && path.Exists)
                {
                    var application = ResolveApplication(path);
                    var serializer = new SerializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                        .Build();

                    var extension = path.Extension.ToLowerInvariant();
                    var directory = path.Directory;
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
                            d.Project = d.Project.Substring(directory.FullName.Length).TrimStart(Path.DirectorySeparatorChar);
                        }
                    }

                    template = serializer.Serialize(descriptions);
                }

                File.WriteAllText("tye.yaml", template);
                console.Out.WriteLine("Created \"tye.yaml\"");
            });

            return command;
        }

        private static Application ResolveApplication(FileInfo file)
        {
            if (!file.Exists)
            {
                throw new FileNotFoundException($"File '{file.FullName}' does not exist");
            }

            switch (file.Extension.ToLower())
            {
                case ".yaml":
                case ".yml":
                    return Application.FromYaml(file.FullName);

                case ".csproj":
                case ".fsproj":
                    return Application.FromProject(file.FullName);

                case ".sln":
                    return Application.FromSolution(file.FullName);

                default:
                    throw new NotSupportedException($"File '{file.FullName}' is not a supported format.");
            }
        }
    }
}
