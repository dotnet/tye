using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public static class ApplicationBuilderExtensions
    {
        public static async Task<bool> TransformProjectsIntoContainersAync(this ApplicationBuilder application, OutputContext output)
        {
            var targets = new[] { "Restore", "Publish" };

            for (var i = 0; i < application.Services.Count; i++)
            {
                var service = application.Services[i];

                if (!(service is ProjectServiceBuilder project))
                {
                    continue;
                }

                if (!await ProjectReader.ReadProjectDetailsAsync(output, project, targets))
                {
                    return false;
                }

                PopulateProjectDefaults(project);

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

            return true;
        }

        public static async Task<bool> BuildProjectsAsync(this ApplicationBuilder application, OutputContext output)
        {
            var targets = new string[] { "Restore", "Build" };
            foreach (var service in application.Services)
            {
                if (service is ProjectServiceBuilder project)
                {
                    if (!await ProjectReader.ReadProjectDetailsAsync(output, project, targets))
                    {
                        return false;
                    }

                    PopulateProjectDefaults(project);
                }
            }

            return true;
        }

        private static void PopulateProjectDefaults(ProjectServiceBuilder project)
        {
            if (project.Bindings.Count == 0 && project.IsAspNet)
            {
                // HTTP is the default binding
                project.Bindings.Add(new BindingBuilder()
                {
                    Protocol = "http"
                });

                project.Bindings.Add(new BindingBuilder()
                {
                    Name = "https",
                    Protocol = "https"
                });
            }
        }
    }
}
