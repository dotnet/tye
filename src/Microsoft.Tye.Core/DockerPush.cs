// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal static class DockerPush
    {
        public static async Task ExecuteAsync(OutputContext output, ContainerEngine containerEngine, string imageName, string imageTag, bool includeLatestTag)
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

            var buildImage = $"{imageName}:{imageTag}";
            var latestImage = includeLatestTag ? $"{imageName}:latest" : string.Empty;

            if (includeLatestTag)
            {
                output.WriteDebugLine("Running 'docker tag'.");
                output.WriteCommandLine("docker", $"tag {buildImage} {latestImage}");
                var tagCapture = output.Capture();
                var tagExitCode = await containerEngine.ExecuteAsync(
                    $"tag {buildImage} {latestImage}",
                    stdOut: tagCapture.StdOut,
                    stdErr: tagCapture.StdErr);

                output.WriteDebugLine($"Done running 'docker tag' exit code: {tagExitCode}");
                if (tagExitCode != 0)
                {
                    throw new CommandException("'docker tag' failed.");
                }
            }

            output.WriteDebugLine("Running 'docker push'.");
            output.WriteCommandLine("docker", $"push {buildImage}");
            var pushCapture = output.Capture();
            var pushExitCode = await containerEngine.ExecuteAsync(
                $"push {buildImage}",
                stdOut: pushCapture.StdOut,
                stdErr: pushCapture.StdErr);

            output.WriteDebugLine($"Done running 'docker push' exit code: {pushExitCode}");
            if (pushExitCode != 0)
            {
                throw new CommandException("'docker push' failed.");
            }

            if (!includeLatestTag)
                return;

            output.WriteDebugLine("Running 'docker push'.");
            output.WriteCommandLine("docker", $"push {latestImage}");
            var pushLatestCapture = output.Capture();
            var pushLatestExitCode = await containerEngine.ExecuteAsync(
                $"push {latestImage}",
                stdOut: pushLatestCapture.StdOut,
                stdErr: pushLatestCapture.StdErr);

            output.WriteDebugLine($"Done running 'docker push' exit code: {pushLatestExitCode}");
            if (pushLatestExitCode != 0)
            {
                throw new CommandException("'docker push' failed.");
            }
        }
    }
}
