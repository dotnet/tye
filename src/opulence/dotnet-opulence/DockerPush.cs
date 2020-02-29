using System;
using System.CommandLine.Invocation;
using System.Threading.Tasks;

namespace Opulence
{
    internal static class DockerPush
    {
        public static async Task ExecuteAsync(OutputContext output, string imageName, string imageTag)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (imageName is null)
            {
                throw new ArgumentNullException(nameof(imageName));
            }

            if (imageTag is null)
            {
                throw new ArgumentNullException(nameof(imageTag));
            }

            output.WriteDebugLine("Running 'docker push'.");
            output.WriteCommandLine("docker", $"push {imageName}:{imageTag}");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
                $"docker",
                $"push {imageName}:{imageTag}",
                stdOut: capture.StdOut,
                stdErr: capture.StdErr);

            output.WriteDebugLine($"Done running 'docker push' exit code: {exitCode}");
            if (exitCode != 0)
            {
                throw new CommandException("'docker push' failed.");
            }
        }
    }
}