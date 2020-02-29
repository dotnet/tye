using System;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Semver;

namespace Opulence
{
    internal static class ProjectReader
    {
        public static async Task ReadProjectDetailsAsync(OutputContext output, FileInfo projectFile, Project project)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (projectFile is null)
            {
                throw new ArgumentNullException(nameof(projectFile));
            }

            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            using (var step = output.BeginStep("Reading Project Details..."))
            {
                await EvaluateMSBuildAsync(output, projectFile, project);

                if (!SemVersion.TryParse(project.Version, out var version))
                {
                    output.WriteInfoLine($"No version or invalid version 'application.Version' found, using default.");
                    version = new SemVersion(0, 1, 0);
                    project.Version = version.ToString();
                }

                step.MarkComplete();
            }
        }

        private static async Task EvaluateMSBuildAsync(OutputContext output, FileInfo projectFile, Project project)
        {
            try
            {
                output.WriteDebugLine("Installing msbuild targets.");
                TargetInstaller.Install(projectFile.FullName);
                output.WriteDebugLine("Installed msbuild targets.");
            }
            catch (Exception ex)
            {
                throw new CommandException("Failed to install targets.", ex);
            }

            var outputFilePath = Path.GetTempFileName();

            try
            {
                var capture = output.Capture();
                var opulenceRoot = Path.GetDirectoryName(typeof(Program).Assembly.Location);

                var restore = string.Empty;
                if (!File.Exists(Path.Combine(projectFile.DirectoryName, "obj", "project.assets.json")))
                {
                    restore = "/restore";
                }

                output.WriteDebugLine("Running 'dotnet msbuild'.");
                output.WriteCommandLine("dotnet", $"msbuild {restore} /t:EvaluateOpulenceProjectInfo \"/p:OpulenceTargetLocation={opulenceRoot}\" \"/p:OpulenceOutputFilePath={outputFilePath}\"");
                var exitCode = await Process.ExecuteAsync(
                    $"dotnet",
                    $"msbuild {restore} /t:EvaluateOpulenceProjectInfo \"/p:OpulenceTargetLocation={opulenceRoot}\" \"/p:OpulenceOutputFilePath={outputFilePath}\"",
                    workingDir: projectFile.DirectoryName,
                    stdOut: capture.StdOut,
                    stdErr: capture.StdErr);

                output.WriteDebugLine($"Done running 'dotnet msbuild' exit code: {exitCode}");
                if (exitCode != 0)
                {
                    throw new CommandException("'dotnet msbuild' failed.");
                }

                var lines = await File.ReadAllLinesAsync(outputFilePath);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (line.StartsWith("version="))
                    {
                        project.Version = line.Substring("version=".Length).Trim();
                        output.WriteDebugLine($"Found application version: {line}");
                        continue;
                    }

                    if (line.StartsWith("tfm"))
                    {
                        project.TargetFramework = line.Substring("tfm=".Length).Trim();
                        output.WriteDebugLine($"Found target framework: {line}");
                        continue;
                    }

                    if (line.StartsWith("frameworks="))
                    {
                        var right = line.Substring("frameworks=".Length).Trim();
                        project.Frameworks.AddRange(right.Split(",").Select(s => new Framework(s)));
                        output.WriteDebugLine($"Found shared frameworks: {line}");
                        continue;
                    }
                }
            }
            finally
            {
                File.Delete(outputFilePath);
            }
        }
    }
}