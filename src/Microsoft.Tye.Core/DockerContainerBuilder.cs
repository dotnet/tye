﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal static class DockerContainerBuilder
    {
        public static async Task BuildContainerImageAsync(OutputContext output, ApplicationBuilder application, ProjectServiceBuilder project, ContainerInfo container)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (container is null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            using var tempFile = TempFile.Create();

            var dockerFilePath = Path.Combine(project.ProjectFile.DirectoryName, "Dockerfile");
            if (File.Exists(dockerFilePath))
            {
                output.WriteDebugLine($"Using existing Dockerfile '{dockerFilePath}'.");
            }
            else
            {
                await DockerfileGenerator.WriteDockerfileAsync(output, application, project, container, tempFile.FilePath);
                dockerFilePath = tempFile.FilePath;
            }

            // We need to know if this is a single-phase or multi-phase Dockerfile because the context directory will be
            // different depending on that choice.
            string contextDirectory;
            if (container.UseMultiphaseDockerfile ?? true)
            {
                contextDirectory = ".";
            }
            else
            {
                var publishOutput = project.Outputs.OfType<ProjectPublishOutput>().FirstOrDefault();
                if (publishOutput is null)
                {
                    throw new InvalidOperationException("We should have published the project for a single-phase Dockerfile.");
                }

                contextDirectory = publishOutput.Directory.FullName;
            }

            output.WriteDebugLine("Running 'docker build'.");
            output.WriteCommandLine("docker", $"build \"{contextDirectory}\" -t {container.ImageName}:{container.ImageTag} -f \"{dockerFilePath}\"");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
                $"docker",
                $"build \"{contextDirectory}\" -t {container.ImageName}:{container.ImageTag} -f \"{dockerFilePath}\"",
                project.ProjectFile.DirectoryName,
                stdOut: capture.StdOut,
                stdErr: capture.StdErr);

            output.WriteDebugLine($"Done running 'docker build' exit code: {exitCode}");
            if (exitCode != 0)
            {
                throw new CommandException("'docker build' failed.");
            }

            output.WriteInfoLine($"Created Docker Image: '{container.ImageName}:{container.ImageTag}'");
            project.Outputs.Add(new DockerImageOutput(container.ImageName!, container.ImageTag!));
        }
    }
}
