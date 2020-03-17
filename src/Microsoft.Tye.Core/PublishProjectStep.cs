// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    // Used to publish a project when using a single-file dockerfile
    public class PublishProjectStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Publishing Project...";

        public override async Task ExecuteAsync(OutputContext output, Application application, ServiceEntry service)
        {
            if (SkipWithoutProject(output, service, out var project))
            {
                return;
            }

            if (SkipWithoutContainerInfo(output, service, out var container))
            {
                return;
            }

            if (container.UseMultiphaseDockerfile != false)
            {
                return;
            }

            var projectFilePath = Path.Combine(application.RootDirectory, project.RelativeFilePath);
            var outputDirectory = Path.Combine(Path.GetDirectoryName(projectFilePath)!, "bin", "Release", project.TargetFramework, "publish");

            output.WriteDebugLine("Running 'dotnet publish'.");
            output.WriteCommandLine("dotnet", $"publish \"{projectFilePath}\" -c Release -o \"{outputDirectory}\"");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
                $"dotnet",
                $"publish \"{projectFilePath}\" -c Release -o \"{outputDirectory}\"",
                application.GetProjectDirectory(project),
                stdOut: capture.StdOut,
                stdErr: capture.StdErr);

            output.WriteDebugLine($"Done running 'dotnet publish' exit code: {exitCode}");
            if (exitCode != 0)
            {
                throw new CommandException("'dotnet publish' failed.");
            }

            output.WriteInfoLine($"Created Publish Output: '{outputDirectory}'");
            service.Outputs.Add(new ProjectPublishOutput(new DirectoryInfo(outputDirectory)));
        }
    }
}
