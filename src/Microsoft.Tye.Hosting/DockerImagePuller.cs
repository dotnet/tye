// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class DockerImagePuller : IApplicationProcessor
    {
        private readonly ILogger _logger;

        public DockerImagePuller(ILogger logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(Application application)
        {
            var images = new HashSet<string>();

            foreach (var s in application.Services)
            {
                if (s.Value.Description.RunInfo is DockerRunInfo docker && docker.DockerFile == null)
                {
                    images.Add(docker.Image);
                }
            }

            // No images, no docker skip it.
            if (images.Count == 0)
            {
                return;
            }

            if (!application.ContainerEngine.IsUsable(out string? unusableReason))
            {
                _logger.LogError($"Unable to pull image: {unusableReason}.");

                throw new CommandException($"Unable to pull image: {unusableReason}.");
            }

            var tasks = new Task[images.Count];
            var index = 0;
            foreach (var image in images)
            {
                tasks[index++] = PullContainerAsync(application, image);
            }

            await Task.WhenAll(tasks);
        }

        private async Task PullContainerAsync(Application application, string image)
        {
            await Task.Yield();

            var result = await application.ContainerEngine.RunAsync(
                                    $"images --filter \"reference={image}\" --format \"{{{{.ID}}}}\"",
                                    throwOnError: false);

            if (result.ExitCode != 0)
            {
                _logger.LogInformation("{Image}: " + result.StandardError, image);
                throw new CommandException("Docker images command failed");
            }

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                _logger.LogInformation("Docker image {image} already installed", image);
                return;
            }

            var command = $"pull {image}";

            _logger.LogInformation("Running docker command {command}", command);

            result = await application.ContainerEngine.RunAsync(
                             command,
                             outputDataReceived: data => _logger.LogInformation("{Image}: " + data, image),
                             errorDataReceived: data => _logger.LogInformation("{Image}: " + data, image),
                             throwOnError: false);

            if (result.ExitCode != 0)
            {
                throw new CommandException("Docker pull command failed");
            }
        }

        public Task StopAsync(Application application)
        {
            return Task.CompletedTask;
        }
    }
}
