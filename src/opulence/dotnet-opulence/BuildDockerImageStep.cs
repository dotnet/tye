using System.Threading.Tasks;

namespace Opulence
{
    public sealed class BuildDockerImageStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Building Docker Image...";

        public string Environment { get; set; } = "production";

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

            if (SkipForEnvironment(output, service, Environment))
            {
                return;
            }

            await DockerContainerBuilder.BuildContainerImageAsync(output, application, service, project, container);
        }
    }
}
