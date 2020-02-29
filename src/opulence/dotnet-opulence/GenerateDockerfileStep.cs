using System.IO;
using System.Threading.Tasks;

namespace Opulence
{
    internal sealed class GenerateDockerfileStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Generating Dockerfile...";

        public bool Force { get; set; }

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

            if (container.UseMultiphaseDockerfile == false)
            {
                throw new CommandException("Generated Dockerfile workflow does not support single-phase Dockerfiles.");
            }

            container.UseMultiphaseDockerfile ??= true;

            var dockerFilePath = Path.Combine(application.GetProjectDirectory(project), "Dockerfile");
            if (File.Exists(dockerFilePath) && !Force)
            {
                throw new CommandException("'Dockerfile' already exists for project. use '--force' to overwrite.");
            }

            File.Delete(dockerFilePath);

            await DockerfileGenerator.WriteDockerfileAsync(output, application, service, project, container, dockerFilePath);
            output.WriteInfoLine($"Generated Dockerfile at '{dockerFilePath}'.");
        }
    }
}