using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public static class ApplicationBuilderExtensions
    {
        public static void TransformProjectsIntoContainers(this ApplicationBuilder application)
        {
            for (var i = 0; i < application.Services.Count; i++)
            {
                var service = application.Services[i];

                if (!(service is ProjectServiceBuilder project))
                {
                    continue;
                }

                static string DetermineContainerImage(ProjectServiceBuilder project)
                {
                    return $"mcr.microsoft.com/dotnet/core/sdk:{project.TargetFrameworkVersion}";
                }

                // We transform the project information into the following docker command:
                // docker run -w /app -v {publishDir}:/app -it {image} dotnet {outputfile}.dll

                var containerImage = DetermineContainerImage(project);
                var outputFileName = project.AssemblyName + ".dll";
                var containerService = new ContainerServiceBuilder(service.Name, containerImage)
                {
                    Replicas = project.Replicas,
                    Args = $"dotnet {outputFileName} {project.Args}",
                    WorkingDirectory = "/app"
                };

                containerService.Volumes.Add(new VolumeBuilder(source: project.PublishDir, name: null, target: "/app"));

                // Make volume mapping works when running as a container
                containerService.Volumes.AddRange(project.Volumes);
                containerService.Bindings.AddRange(project.Bindings);
                containerService.EnvironmentVariables.AddRange(project.EnvironmentVariables);

                application.Services[i] = containerService;
            }
        }
    }
}
