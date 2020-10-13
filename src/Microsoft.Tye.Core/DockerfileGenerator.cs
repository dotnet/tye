// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public static class DockerfileGenerator
    {
        public static async Task WriteDockerfileAsync(OutputContext output, ApplicationBuilder application, DotnetProjectServiceBuilder project, ContainerInfo container, string filePath)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (container is null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            await using var stream = File.OpenWrite(filePath);
            await using var writer = new StreamWriter(stream, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: -1, leaveOpen: true);

            var entryPoint = project.AssemblyName;
            output.WriteDebugLine($"Writing Dockerfile to '{filePath}'.");
            if (container.UseMultiphaseDockerfile ?? true)
            {
                await WriteMultiphaseDockerfileAsync(writer, entryPoint, container);
            }
            else
            {
                await WriteLocalPublishDockerfileAsync(writer, entryPoint, container);
            }
            output.WriteDebugLine("Done writing Dockerfile.");
        }

        private static async Task WriteMultiphaseDockerfileAsync(StreamWriter writer, string applicationEntryPoint, ContainerInfo container)
        {
            await writer.WriteLineAsync($"FROM {container.BuildImageName}:{container.BuildImageTag} as SDK");
            await writer.WriteLineAsync($"WORKDIR /src");
            await writer.WriteLineAsync($"COPY . .");
            await writer.WriteLineAsync($"RUN dotnet publish -c Release -o /out");
            await writer.WriteLineAsync($"FROM {container.BaseImageName}:{container.BaseImageTag} as RUNTIME");
            await writer.WriteLineAsync($"WORKDIR /app");
            await writer.WriteLineAsync($"COPY --from=SDK /out .");
            await writer.WriteLineAsync($"ENTRYPOINT [\"dotnet\", \"{applicationEntryPoint}.dll\"]");
        }

        private static async Task WriteLocalPublishDockerfileAsync(StreamWriter writer, string applicationEntryPoint, ContainerInfo container)
        {
            await writer.WriteLineAsync($"FROM {container.BaseImageName}:{container.BaseImageTag}");
            await writer.WriteLineAsync($"WORKDIR /app");
            await writer.WriteLineAsync($"COPY . /app");
            await writer.WriteLineAsync($"ENTRYPOINT [\"dotnet\", \"{applicationEntryPoint}.dll\"]");
        }

        public static void ApplyContainerDefaults(ApplicationBuilder application, DotnetProjectServiceBuilder project, ContainerInfo container)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (container is null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (string.IsNullOrEmpty(container.BaseImageTag) && (project.TargetFrameworkName == "netcoreapp" || project.TargetFrameworkName == "net"))
            {
                container.BaseImageTag = project.TargetFrameworkVersion;
            }

            if (string.IsNullOrEmpty(container.BaseImageTag))
            {
                throw new CommandException($"Unsupported TFM {project.TargetFramework}.");
            }

            if (string.IsNullOrEmpty(container.BaseImageName))
            {
                if (TagIs50OrNewer(container.BaseImageTag))
                {
                    container.BaseImageName = project.IsAspNet ? "mcr.microsoft.com/dotnet/aspnet" : "mcr.microsoft.com/dotnet/runtime";
                }
                else
                {
                    container.BaseImageName = project.IsAspNet ? "mcr.microsoft.com/dotnet/core/aspnet": "mcr.microsoft.com/dotnet/core/runtime";
                }
            }

            container.BuildImageTag ??= project.TargetFrameworkVersion;

            if (string.IsNullOrEmpty(container.BuildImageName))
            {
                container.BuildImageName = TagIs50OrNewer(container.BuildImageTag) ? "mcr.microsoft.com/dotnet/sdk" : "mcr.microsoft.com/dotnet/core/sdk";
            }

            if (container.ImageName == null && application.Registry?.Hostname == null)
            {
                container.ImageName ??= project.Name.ToLowerInvariant();
            }
            else if (container.ImageName == null && application.Registry?.Hostname != null)
            {
                container.ImageName ??= $"{application.Registry?.Hostname}/{project.Name.ToLowerInvariant()}";
            }

            container.ImageTag ??= project.Version?.Replace("+", "-") ?? "latest";

            // Disable color in the logs
            project.EnvironmentVariables.Add(new EnvironmentVariableBuilder("DOTNET_LOGGING__CONSOLE__DISABLECOLORS") { Value = "true" });
        }

        public static void ApplyContainerDefaults(ApplicationBuilder application, DockerFileServiceBuilder project, ContainerInfo container)
        {
            if (container.ImageName == null && application.Registry?.Hostname == null)
            {
                container.ImageName ??= project.Name.ToLowerInvariant();
            }
            else if (container.ImageName == null && application.Registry?.Hostname != null)
            {
                container.ImageName ??= $"{application.Registry?.Hostname}/{project.Name.ToLowerInvariant()}";
            }

            container.ImageTag ??= "latest";
        }

        public static bool TagIs50OrNewer(string tag)
        {
            if (string.Equals("latest", tag))
            {
                return true;
            }

            if (!Version.TryParse(tag, out var version))
            {
                throw new CommandException($"Could not determine version of docker image for tag: {tag}.");
            }

            return version.Major >= 5;
        }
    }
}
