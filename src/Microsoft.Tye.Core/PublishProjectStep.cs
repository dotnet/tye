// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    // Used to publish a project when using a single-phase Dockerfile
    public class PublishProjectStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Publishing Project...";

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
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

            // NOTE: we're intentionally not cleaning up here. It's the responsibility of whomever consumes
            // the publish output to do cleanup.
            var outputDirectory = TempDirectory.Create();

            output.WriteDebugLine("Running 'dotnet publish'.");
            output.WriteCommandLine("dotnet", $"publish \"{project.ProjectFile.FullName}\" -c Release -o \"{outputDirectory.DirectoryPath}\"");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
                $"dotnet",
                $"publish \"{project.ProjectFile.FullName}\" -c Release -o \"{outputDirectory.DirectoryPath}\"",
                project.ProjectFile.DirectoryName,
                stdOut: capture.StdOut,
                stdErr: capture.StdErr);

            output.WriteDebugLine($"Done running 'dotnet publish' exit code: {exitCode}");
            if (exitCode != 0)
            {
                outputDirectory.Dispose();
                throw new CommandException("'dotnet publish' failed.");
            }

            output.WriteDebugLine($"Created Publish Output: '{outputDirectory}'");
            service.Outputs.Add(new ProjectPublishOutput(outputDirectory.DirectoryInfo));
        }
    }
}
