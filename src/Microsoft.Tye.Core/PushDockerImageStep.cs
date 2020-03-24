// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class PushDockerImageStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Pushing Docker Image...";

        public string Environment { get; set; } = "production";

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            if (SkipWithoutProject(output, service, out var _))
            {
                return;
            }

            if (SkipWithoutContainerInfo(output, service, out var _))
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
