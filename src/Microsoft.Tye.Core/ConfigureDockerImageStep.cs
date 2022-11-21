using System.Threading.Tasks;
using Microsoft.Tye;

namespace Tye
{
    public sealed class ConfigureDockerImageStep : ApplicationExecutor.ServiceStep
    {
        public override string DisplayText => "Configuring Docker Image...";

        public override Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            if (SkipWithoutProject(output, service, out var project))
            {
                return Task.CompletedTask;
            }

            if (SkipWithoutContainerInfo(output, service, out var container))
            {
                return Task.CompletedTask;
            }

            if (!application.ContainerEngine.IsUsable(out string? unusableReason))
            {
                throw new CommandException($"Cannot generate a docker image for '{service.Name}' because {unusableReason}.");
            }

            if (project is DotnetProjectServiceBuilder dotnetProject)
            {
                // For configuring the docker image, we have to assume that the images already exist. Just add them to the project.
                dotnetProject.Outputs.Add(new DockerImageOutput(container.ImageName!, container.ImageTag!));
            }

            return Task.CompletedTask;
        }
    }
}
