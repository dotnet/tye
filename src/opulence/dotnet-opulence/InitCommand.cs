using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;

namespace Opulence
{
    internal static class InitCommand
    {
        public static Command Create()
        {
            var command = new Command("init", "Initialize repo")
            {
                StandardOptions.Verbosity,
                new Option(new[] { "-d", "--directory", }, "Directory to Initialize")
                {
                    Argument = new Argument<DirectoryInfo>(() =>
                    {
                        return new DirectoryInfo(Environment.CurrentDirectory);
                    }).ExistingOnly(),
                },
            };

            command.Handler = CommandHandler.Create<IConsole, Verbosity, DirectoryInfo>((console, verbosity, directory) =>
            {
                var output = new OutputContext(console, verbosity);
                return ExecuteAsync(output, directory);
            });

            return command;
        }

        private static async Task ExecuteAsync(OutputContext output, DirectoryInfo directory)
        {
            output.WriteBanner();

            string? solutionFilePath = null;
            string? opulenceFilePath = null;

            using (var step = output.BeginStep("Looking For Existing Config..."))
            {
                opulenceFilePath = DirectorySearch.AscendingSearch(directory.FullName, "Opulence.csx");
                if (opulenceFilePath != null)
                {
                    output.WriteInfoLine($"Found 'Opulence.csx' at '{opulenceFilePath}'");
                    step.MarkComplete();
                    return;
                }
                else
                {
                    output.WriteInfoLine("Not Found");
                    step.MarkComplete();
                }
            }

            using (var step = output.BeginStep("Looking For .sln File..."))
            {
                solutionFilePath = DirectorySearch.AscendingWildcardSearch(directory.FullName, "*.sln").FirstOrDefault()?.FullName;
                if (opulenceFilePath == null && 
                    solutionFilePath != null && 
                    output.Confirm($"Use '{Path.GetDirectoryName(solutionFilePath)}' as Root?"))
                {
                    opulenceFilePath = Path.Combine(Path.GetDirectoryName(solutionFilePath)!, "Opulence.csx");
                    step.MarkComplete();
                }
                else 
                {
                    output.WriteInfoLine("Not Found.");
                    step.MarkComplete();
                }
            }

            if (opulenceFilePath == null && 
                Path.GetFullPath(directory.FullName) != Path.GetFullPath(Environment.CurrentDirectory))
            {
                // User specified a directory other than the current one
                using (var step = output.BeginStep("Trying Project Directory..."))
                {
                    if (output.Confirm("Use Project Directory as Root?"))
                    {
                        opulenceFilePath = Path.Combine(directory.FullName, "Opulence.csx");
                    }

                    step.MarkComplete();
                }
            }

            if (opulenceFilePath == null)
            {
                using (var step = output.BeginStep("Trying Current Directory..."))
                {
                    if (output.Confirm("Use Current Directory as Root?"))
                    {
                        opulenceFilePath = Path.Combine(directory.FullName, "Opulence.csx");
                    }

                    step.MarkComplete();
                }
            }

            if (opulenceFilePath == null)
            {
                throw new CommandException("Cannot Determine Root Directory.");
            }

            using (var step = output.BeginStep("Writing Opulence.csx ..."))
            {
                var hostname = output.Prompt("Enter the Container Registry (ex: 'example.azurecr.io' for Azure or 'example' for dockerhub)");

                var services = new List<(string, string)>();
                if (solutionFilePath != null && output.Confirm($"Use solution file '{solutionFilePath}' to initialize services?"))
                {
                    services.AddRange(ReadServicesFromSolution(solutionFilePath));
                    services.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                }

                using var stream = File.OpenWrite(opulenceFilePath);
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

                await WriteOpulenceConfigAsync(writer, hostname, services);

                output.WriteInfoLine($"Initialized Opulence Config at '{opulenceFilePath}'.");
                step.MarkComplete();
            }
        }

        private static IEnumerable<(string, string)> ReadServicesFromSolution(string solutionFilePath)
        {
            SolutionFile solution;
            try
            {
                solution = SolutionFile.Parse(solutionFilePath);
            }
            catch (Exception ex)
            {
                throw new CommandException($"Parsing solution file '{solutionFilePath}' failed.", ex);
            }

            for (var i = 0; i < solution.ProjectsInOrder.Count; i++)
            {
                var project = solution.ProjectsInOrder[i];
                if (string.Equals(Path.GetExtension(project.RelativePath), ".csproj", StringComparison.Ordinal))
                {
                    var fileName = Path.GetFileNameWithoutExtension(project.RelativePath.Replace('\\', Path.DirectorySeparatorChar));

                    yield return (Names.NormalizeToFriendly(fileName), Names.NormalizeToDns(fileName));
                }
            }
        }

        private static async Task WriteOpulenceConfigAsync(TextWriter writer, string hostname, List<(string, string)> services)
        {
            await writer.WriteLineAsync("#r \"Opulence\"");
            await writer.WriteLineAsync();

            await writer.WriteLineAsync($"using Opulence;");
            await writer.WriteLineAsync();

            await writer.WriteLineAsync($"public class Application");
            await writer.WriteLineAsync($"{{");
            await writer.WriteLineAsync($"    public ApplicationGlobals Globals {{ get; }} = new ApplicationGlobals()");
            await writer.WriteLineAsync($"    {{");
            await writer.WriteLineAsync($"        Registry = new ContainerRegistry(\"{hostname}\"),");
            await writer.WriteLineAsync($"    }};");
            await writer.WriteLineAsync();

            await writer.WriteLineAsync($"    // Define more services and dependencies here as your application grows.");
            for (var i = 0; i < services.Count; i++)
            {
                await writer.WriteLineAsync($"    public Service {services[i].Item1} {{ get; }} = new Service(\"{services[i].Item2}\");");

                if (i + 1 < services.Count)
                {
                    await writer.WriteLineAsync();
                }
            }

            await writer.WriteLineAsync($"}}");
            await writer.WriteLineAsync();

            await writer.WriteLineAsync($"Pipeline.Configure<Application>(app =>");
            await writer.WriteLineAsync($"{{");
            await writer.WriteLineAsync($"    // Configure your service bindings here with code.");
            await writer.WriteLineAsync($"}});");
        } 
    }
}