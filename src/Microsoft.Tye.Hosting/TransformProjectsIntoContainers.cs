﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    using System.Linq;

    public class TransformProjectsIntoContainers : IApplicationProcessor
    {
        private readonly ILogger _logger;

        public TransformProjectsIntoContainers(ILogger logger)
        {
            _logger = logger;
        }

        public Task StartAsync(Application application)
        {
            // This transforms a ProjectRunInfo into a container
            var tasks = new List<Task>();
            foreach (var s in application.Services.Values)
            {
                if (s.Description.RunInfo is ProjectRunInfo project)
                {
                    tasks.Add(TransformProjectToContainer(s, project));
                }
            }

            return Task.WhenAll(tasks);
        }

        private async Task TransformProjectToContainer(Service service, ProjectRunInfo project)
        {
            var serviceDescription = service.Description;
            var serviceName = serviceDescription.Name;

            service.Status.ProjectFilePath = project.ProjectFile.FullName;
            var targetFramework = project.TargetFramework;

            // Sometimes building can fail because of file locking (like files being open in VS)
            _logger.LogInformation("Publishing project {ProjectFile}", service.Status.ProjectFilePath);

            var buildArgs = project.BuildProperties.Aggregate(string.Empty, (current, property) => current + $" /p:{property.Key}={property.Value}").TrimStart();

            var publishCommand = $"publish \"{service.Status.ProjectFilePath}\" --framework {targetFramework} {buildArgs} /nologo";

            service.Logs.OnNext($"dotnet {publishCommand}");

            var buildResult = await ProcessUtil.RunAsync("dotnet", publishCommand, throwOnError: false);

            service.Logs.OnNext(buildResult.StandardOutput);

            if (buildResult.ExitCode != 0)
            {
                _logger.LogInformation("Publishing {ProjectFile} failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, service.Status.ProjectFilePath, buildResult.ExitCode);

                // Null out the RunInfo so that
                serviceDescription.RunInfo = null;
                return;
            }

            // We transform the project information into the following docker command:
            // docker run -w /app -v {publishDir}:/app -it {image} dotnet {outputfile}.dll

            var containerImage = DetermineContainerImage(project);
            var outputFileName = project.AssemblyName + ".dll";
            var dockerRunInfo = new DockerRunInfo(containerImage, $"dotnet {outputFileName} {project.Args}")
            {
                WorkingDirectory = "/app"
            };

            dockerRunInfo.VolumeMappings.Add(new DockerVolume(source: project.PublishOutputPath, name: null, target: "/app"));

            // Make volume mapping works when running as a container
            dockerRunInfo.VolumeMappings.AddRange(project.VolumeMappings);

            // Change the project into a container info
            serviceDescription.RunInfo = dockerRunInfo;
        }

        private static string DetermineContainerImage(ProjectRunInfo project)
        {
            return $"mcr.microsoft.com/dotnet/core/sdk:{project.TargetFrameworkVersion}";
        }

        public Task StopAsync(Application application)
        {
            return Task.CompletedTask;
        }
    }
}
