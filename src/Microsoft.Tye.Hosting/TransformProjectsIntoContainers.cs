// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class TransformProjectsIntoContainers : IApplicationProcessor
    {
        private readonly ILogger _logger;

        public TransformProjectsIntoContainers(ILogger logger)
        {
            _logger = logger;
        }

        public Task StartAsync(Model.Application application)
        {
            // This transforms a ProjectRunInfo into a container
            var tasks = new List<Task>();
            foreach (var s in application.Services.Values)
            {
                if (s.Description.RunInfo is ProjectRunInfo project)
                {
                    tasks.Add(TransformProjectToContainer(application, s, project));
                }
            }

            return Task.WhenAll(tasks);
        }

        private async Task TransformProjectToContainer(Model.Application application, Model.Service service, ProjectRunInfo project)
        {
            var serviceDescription = service.Description;
            var serviceName = serviceDescription.Name;

            var expandedProject = Environment.ExpandEnvironmentVariables(project.Project);
            var fullProjectPath = Path.GetFullPath(Path.Combine(application.ContextDirectory, expandedProject));
            service.Status.ProjectFilePath = fullProjectPath;

            // Sometimes building can fail because of file locking (like files being open in VS)
            _logger.LogInformation("Publishing project {ProjectFile}", service.Status.ProjectFilePath);

            service.Logs.OnNext($"dotnet publish \"{service.Status.ProjectFilePath}\" /nologo");

            var buildResult = await ProcessUtil.RunAsync("dotnet", $"publish \"{service.Status.ProjectFilePath}\" /nologo", throwOnError: false);

            service.Logs.OnNext(buildResult.StandardOutput);

            if (buildResult.ExitCode != 0)
            {
                _logger.LogInformation("Publishing {ProjectFile} failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, service.Status.ProjectFilePath, buildResult.ExitCode);

                // Null out the RunInfo so that
                serviceDescription.RunInfo = null;
                return;
            }

            var targetFramework = GetTargetFramework(service.Status.ProjectFilePath);

            // We transform the project information into the following docker command:
            // docker run -w /app -v {projectDir}:/app -it {image} dotnet /app/bin/Debug/{tfm}/publish/{outputfile}.dll
            var containerImage = DetermineContainerImage(targetFramework);
            var outputFileName = Path.GetFileNameWithoutExtension(service.Status.ProjectFilePath) + ".dll";
            var dockerRunInfo = new DockerRunInfo(containerImage, $"dotnet /app/bin/Debug/{targetFramework}/publish/{outputFileName} {project.Args}")
            {
                WorkingDirectory = "/app"
            };
            dockerRunInfo.VolumeMappings[Path.GetDirectoryName(service.Status.ProjectFilePath)!] = "/app";

            // Make volume mapping works when running as a container
            foreach (var mapping in project.VolumeMappings)
            {
                dockerRunInfo.VolumeMappings[mapping.Key] = mapping.Value;
            }

            // Change the project into a container info
            serviceDescription.RunInfo = dockerRunInfo;
        }

        private static string DetermineContainerImage(string targetFramework)
        {
            // TODO: Determine the base image from the tfm
            return "mcr.microsoft.com/dotnet/core/sdk:3.1-buster";
        }

        private static string GetTargetFramework(string? projectFilePath)
        {
            // TODO: Use msbuild to get the target path
            var debugOutputPath = Path.Combine(Path.GetDirectoryName(projectFilePath)!, "bin", "Debug");

            var tfms = Directory.Exists(debugOutputPath) ? Directory.GetDirectories(debugOutputPath) : Array.Empty<string>();

            return tfms.Select(tfm => new DirectoryInfo(tfm).Name).FirstOrDefault() ?? "netcoreapp3.1";
        }

        public Task StopAsync(Model.Application application)
        {
            return Task.CompletedTask;
        }
    }
}
