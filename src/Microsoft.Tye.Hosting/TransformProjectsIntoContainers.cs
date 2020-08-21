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
        private Lazy<TempDirectory> _certificateDirectory;

        public TransformProjectsIntoContainers(ILogger logger)
        {
            _logger = logger;
            _certificateDirectory = new Lazy<TempDirectory>(() => TempDirectory.Create(preferUserDirectoryOnMacOS: true));
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
                WorkingDirectory = "/app",
                IsAspNet = project.IsAspNet
            };

            dockerRunInfo.VolumeMappings.Add(new DockerVolume(source: project.PublishOutputPath, name: null, target: "/app"));

            // Make volume mapping works when running as a container
            dockerRunInfo.VolumeMappings.AddRange(project.VolumeMappings);

            // This is .NET specific
            var userSecretStore = GetUserSecretsPathFromSecrets();

            if (!string.IsNullOrEmpty(userSecretStore))
            {
                // Map the user secrets on this drive to user secrets
                dockerRunInfo.VolumeMappings.Add(new DockerVolume(source: userSecretStore, name: null, target: "/root/.microsoft/usersecrets:ro"));
            }

            // Default to development environment
            serviceDescription.Configuration.Add(new EnvironmentVariable("DOTNET_ENVIRONMENT", "Development"));

            // Remove the color codes from the console output
            serviceDescription.Configuration.Add(new EnvironmentVariable("DOTNET_LOGGING__CONSOLE__DISABLECOLORS", "true"));

            if (project.IsAspNet)
            {
                serviceDescription.Configuration.Add(new EnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development"));
                serviceDescription.Configuration.Add(new EnvironmentVariable("ASPNETCORE_LOGGING__CONSOLE__DISABLECOLORS", "true"));
            }

            // If we have an https binding then export the dev cert and mount the volume into the container
            if (serviceDescription.Bindings.Any(b => string.Equals(b.Protocol, "https", StringComparison.OrdinalIgnoreCase)))
            {
                // We export the developer certificate from this machine
                var certPassword = Guid.NewGuid().ToString();
                var certificateDirectory = _certificateDirectory.Value;
                var certificateFilePath = Path.Combine("\"" + certificateDirectory.DirectoryPath, project.AssemblyName + ".pfx\"");
                await ProcessUtil.RunAsync("dotnet", $"dev-certs https -ep {certificateFilePath} -p {certPassword}");
                serviceDescription.Configuration.Add(new EnvironmentVariable("Kestrel__Certificates__Development__Password", certPassword));

                // Certificate Path: https://github.com/dotnet/aspnetcore/blob/a9d702624a02ad4ebf593d9bf9c1c69f5702a6f5/src/Servers/Kestrel/Core/src/KestrelConfigurationLoader.cs#L419
                dockerRunInfo.VolumeMappings.Add(new DockerVolume(source: certificateDirectory.DirectoryPath, name: null, target: "/root/.aspnet/https:ro"));
            }

            // Change the project into a container info
            serviceDescription.RunInfo = dockerRunInfo;
        }

        private static string DetermineContainerImage(ProjectRunInfo project)
        {
            string baseImage;
            if (!string.IsNullOrEmpty(project.ContainerBaseImage))
            {
                baseImage = project.ContainerBaseImage;
            }
            else
            {
                baseImage = project.IsAspNet ? "mcr.microsoft.com/dotnet/core/aspnet" : "mcr.microsoft.com/dotnet/core/runtime";
            }

            var baseImageTag = !string.IsNullOrEmpty(project.ContainerBaseTag) ? project.ContainerBaseTag : project.TargetFrameworkVersion;

            return $"{baseImage}:{baseImageTag}";
        }

        public Task StopAsync(Application application)
        {
            if (_certificateDirectory.IsValueCreated)
            {
                _certificateDirectory.Value.Dispose();
            }

            return Task.CompletedTask;
        }

        private static string? GetUserSecretsPathFromSecrets()
        {
            // This is the logic used to determine the user secrets path
            // See https://github.com/dotnet/extensions/blob/64140f90157fec1bfd8aeafdffe8f30308ccdf41/src/Configuration/Config.UserSecrets/src/PathHelper.cs#L27
            const string userSecretsFallbackDir = "DOTNET_USER_SECRETS_FALLBACK_DIR";

            // For backwards compat, this checks env vars first before using Env.GetFolderPath
            var appData = Environment.GetEnvironmentVariable("APPDATA");
            var root = appData                                                                   // On Windows it goes to %APPDATA%\Microsoft\UserSecrets\
                       ?? Environment.GetEnvironmentVariable("HOME")                             // On Mac/Linux it goes to ~/.microsoft/usersecrets/
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                       ?? Environment.GetEnvironmentVariable(userSecretsFallbackDir);            // this fallback is an escape hatch if everything else fails

            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            return !string.IsNullOrEmpty(appData)
                ? Path.Combine(root, "Microsoft", "UserSecrets")
                : Path.Combine(root, ".microsoft", "usersecrets");
        }
    }
}
