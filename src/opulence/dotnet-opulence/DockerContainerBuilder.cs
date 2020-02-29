using System;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace Opulence
{
    internal static class DockerContainerBuilder
    {
        public static async Task BuildContainerImageAsync(OutputContext output, Application application, ServiceEntry service, Project project, ContainerInfo container)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (service is null)
            {
                throw new ArgumentNullException(nameof(service));
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

            var dockerFilePath = Path.Combine(application.GetProjectDirectory(project), Path.GetDirectoryName(project.RelativeFilePath)!, "Dockerfile");
            if (File.Exists(dockerFilePath))
            {
                output.WriteDebugLine($"Using existing dockerfile '{dockerFilePath}'.");
            }
            else
            {
                await DockerfileGenerator.WriteDockerfileAsync(output, application, service, project, container, tempFile.FilePath);
                dockerFilePath = tempFile.FilePath;
            }

            output.WriteDebugLine("Running 'docker build'.");
            output.WriteCommandLine("docker", $"build . -t {container.ImageName}:{container.ImageTag} -f \"{dockerFilePath}\"");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
                $"docker",
                $"build . -t {container.ImageName}:{container.ImageTag} -f \"{dockerFilePath}\"",
                application.GetProjectDirectory(project),
                stdOut: capture.StdOut,
                stdErr: capture.StdErr);

            output.WriteDebugLine($"Done running 'docker build' exit code: {exitCode}");
            if (exitCode != 0)
            {
                throw new CommandException("'docker build' failed.");
            }

            output.WriteInfoLine($"Created Docker Image: {container.ImageName}:{container.ImageTag}");
            service.Outputs.Add(new DockerImageOutput(container.ImageName!, container.ImageTag!));
        }
    }
}
