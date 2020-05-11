// Licensed to the .NET Foundation under one or more agreements.
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

            string contextDirectory;
            var dockerFilePath = Path.Combine(project.ProjectFile.DirectoryName, "Dockerfile");

            TempFile? tempFile = null;
            TempDirectory? tempDirectory = null;

            try
            {
                // We need to know if this is a single-phase or multi-phase Dockerfile because the context directory will be
                // different depending on that choice.
                //
                // For the cases where generate a Dockerfile, we have the constraint that we need
                // to place it on the same drive (Windows) as the docker context.
                if (container.UseMultiphaseDockerfile ?? true)
                {
                    // For a multi-phase Docker build, the context is always the project directory.
                    contextDirectory = ".";

                    if (File.Exists(dockerFilePath))
                    {
                        output.WriteDebugLine($"Using existing Dockerfile '{dockerFilePath}'.");
                    }
                    else
                    {
                        // We need to write the file, let's stick it under obj.
                        Directory.CreateDirectory(project.IntermediateOutputPath);
                        dockerFilePath = Path.Combine(project.IntermediateOutputPath, "Dockerfile");

                        // Clean up file when done building image
                        tempFile = new TempFile(dockerFilePath);

                        await DockerfileGenerator.WriteDockerfileAsync(output, application, project, container, tempFile.FilePath);
                    }
                }
                else
                {
                    // For a single-phase Docker build the context is always the directory containing the publish
                    // output. We need to put the Dockerfile in the context directory so it's on the same drive (Windows).
                    var publishOutput = project.Outputs.OfType<ProjectPublishOutput>().FirstOrDefault();
                    if (publishOutput is null)
                    {
                        throw new InvalidOperationException("We should have published the project for a single-phase Dockerfile.");
                    }

                    contextDirectory = publishOutput.Directory.FullName;

                    // Clean up directory when done building image
                    tempDirectory = new TempDirectory(publishOutput.Directory);

                    if (File.Exists(dockerFilePath))
                    {
                        output.WriteDebugLine($"Using existing Dockerfile '{dockerFilePath}'.");
                        File.Copy(dockerFilePath, Path.Combine(contextDirectory, "Dockerfile"));
                        dockerFilePath = Path.Combine(contextDirectory, "Dockerfile");
                    }
                    else
                    {
                        // No need to clean up, it's in a directory we're already cleaning up.
                        dockerFilePath = Path.Combine(contextDirectory, "Dockerfile");
                        await DockerfileGenerator.WriteDockerfileAsync(output, application, project, container, dockerFilePath);
                    }
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
            finally
            {
                tempDirectory?.Dispose();
                tempFile?.Dispose();
            }
        }
    }
}
