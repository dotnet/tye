using System;
using System.Collections.Generic;
using System.Linq;
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
                if (s.Value.Description.RunInfo is DockerRunInfo docker)
                {
                    images.Add(docker.Image);
                }
            }

            // No images, no docker skip it.
            if (images.Count == 0)
            {
                return;
            }

            if (!await DockerDetector.Instance.IsDockerInstalled.Value)
            {
                _logger.LogError("Unable to detect docker installation. Docker is not installed.");

                throw new CommandException("Docker is not installed.");
            }

            if (!await DockerDetector.Instance.IsDockerConnectedToDaemon.Value)
            {
                _logger.LogError("Unable to connect to docker daemon. Docker is not running.");

                throw new CommandException("Docker is not running.");
            }

            var tasks = new Task[images.Count];
            var index = 0;
            foreach (var image in images)
            {
                tasks[index++] = PullContainerAsync(image);
            }

            await Task.WhenAll(tasks);
        }

        private async Task PullContainerAsync(string image)
        {
            await Task.Yield();

            var command = $"pull {image}";

            _logger.LogInformation("Running docker command {command}", command);

            var result = await ProcessUtil.RunAsync(
                           "docker",
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
