// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class BuildDockerImageStep : ApplicationExecutor.ServiceStep
    {
        public override string DisplayText => "Building Docker Image...";

        public string Environment { get; set; } = "production";

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            if (SkipWithoutDotnetProject(output, service, out var project))
            {
                await HandleWithDockerFile(output, application, service);
                return;
            }

            if (SkipWithoutContainerInfo(output, service, out var container))
            {
                return;
            }

            if (!await DockerDetector.Instance.IsDockerInstalled.Value)
            {
                throw new CommandException($"Cannot generate a docker image for '{service.Name}' because docker is not installed.");
            }

            if (!await DockerDetector.Instance.IsDockerConnectedToDaemon.Value)
            {
                throw new CommandException($"Cannot generate a docker image for '{service.Name}' because docker is not running.");
            }

            await DockerContainerBuilder.BuildContainerImageAsync(output, application, project, container);
        }

        private async Task HandleWithDockerFile(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            if (SkipWithoutDockerFile(output, service, out var project))
            {
                return;
            }

            if (!await DockerDetector.Instance.IsDockerInstalled.Value)
            {
                throw new CommandException($"Cannot generate a docker image for '{service.Name}' because docker is not installed.");
            }

            if (!await DockerDetector.Instance.IsDockerConnectedToDaemon.Value)
            {
                throw new CommandException($"Cannot generate a docker image for '{service.Name}' because docker is not running.");
            }

            await DockerContainerBuilder.BuildContainerImageFromDockerFile(output, application, project);
        }
    }
}
