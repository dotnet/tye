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
            await writer.WriteLineAsync($"FROM {container.BuildImage.Name}:{container.BuildImage.Tag} as SDK");
            await writer.WriteLineAsync($"WORKDIR /src");
            await writer.WriteLineAsync($"COPY . .");
            await writer.WriteLineAsync($"RUN dotnet publish -c Release -o /out");
            await writer.WriteLineAsync($"FROM {container.BaseImage.Name}:{container.BaseImage.Tag} as RUNTIME");
            await writer.WriteLineAsync($"WORKDIR /app");
            await writer.WriteLineAsync($"COPY --from=SDK /out .");
            await writer.WriteLineAsync($"ENTRYPOINT [\"dotnet\", \"{applicationEntryPoint}.dll\"]");
        }

        private static async Task WriteLocalPublishDockerfileAsync(StreamWriter writer, string applicationEntryPoint, ContainerInfo container)
        {
            await writer.WriteLineAsync($"FROM {container.BaseImage.Name}:{container.BaseImage.Tag}");
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

            if (string.IsNullOrEmpty(container.BaseImage.Tag) && (project.TargetFrameworkName == "netcoreapp" || project.TargetFrameworkName == "net"))
            {
                container.BaseImage.Tag = project.TargetFrameworkVersion;
            }

            if (string.IsNullOrEmpty(container.BaseImage.Tag))
            {
                throw new CommandException($"Unsupported TFM {project.TargetFramework}.");
            }

            if (string.IsNullOrEmpty(container.BaseImage.Name))
            {
                if (TagIs50OrNewer(container.BaseImage.Tag))
                {
                    container.BaseImage.Name = project.IsAspNet ? "mcr.microsoft.com/dotnet/aspnet" : "mcr.microsoft.com/dotnet/runtime";
                }
                else
                {
                    container.BaseImage.Name = project.IsAspNet ? "mcr.microsoft.com/dotnet/core/aspnet" : "mcr.microsoft.com/dotnet/core/runtime";
                }
            }

            container.BuildImage.Name ??= project.TargetFrameworkVersion;

            if (string.IsNullOrEmpty(container.BuildImage.Name))
            {
                container.BuildImage.Name = TagIs50OrNewer(container.BuildImage.Tag) ? "mcr.microsoft.com/dotnet/sdk" : "mcr.microsoft.com/dotnet/core/sdk";
            }

            if (container.Image.Name == null && application.Registry?.Hostname == null)
            {
                container.Image.Name ??= project.Name.ToLowerInvariant();
            }
            else if (container.Image.Name == null && application.Registry?.Hostname != null)
            {
                container.Image.Name ??= $"{application.Registry?.Hostname}/{project.Name.ToLowerInvariant()}";
            }

            container.Image.Tag ??= project.Version?.Replace("+", "-") ?? "latest";

            // Disable color in the logs
            project.EnvironmentVariables.Add(new EnvironmentVariableBuilder("DOTNET_LOGGING__CONSOLE__DISABLECOLORS") { Value = "true" });
        }

        public static void ApplyContainerDefaults(ApplicationBuilder application, DockerFileServiceBuilder project, ContainerInfo container)
        {
            if (container.Image.Name == null && application.Registry?.Hostname == null)
            {
                container.Image.Name ??= project.Name.ToLowerInvariant();
            }
            else if (container.Image.Name == null && application.Registry?.Hostname != null)
            {
                container.Image.Name ??= $"{application.Registry?.Hostname}/{project.Name.ToLowerInvariant()}";
            }

            container.Image.Tag ??= "latest";
        }

        public static bool TagIs50OrNewer(string? tag)
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
