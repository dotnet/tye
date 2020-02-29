using System.Linq;
using System.Threading.Tasks;

namespace Opulence
{
    internal sealed class PushDockerImageStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Pushing Docker Image...";

        public string Environment { get; set; } = "production";

        public override async Task ExecuteAsync(OutputContext output, Application application, ServiceEntry service)
        {
            if (SkipWithoutProject(output, service, out var _))
            {
                return;
            }

            if (SkipWithoutContainerInfo(output, service, out var _))
            {
                return;
            }

            if (SkipForEnvironment(output, service, Environment))
            {
                return;
            }

            foreach (var image in service.Outputs.OfType<DockerImageOutput>())
            {
                await DockerPush.ExecuteAsync(output, image.ImageName, image.ImageTag);
                output.WriteInfoLine($"Pushed docker image: '{image.ImageName}:{image.ImageTag}'");
            }
        }
    }
}