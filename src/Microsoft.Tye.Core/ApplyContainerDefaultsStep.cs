// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class ApplyContainerDefaultsStep : ApplicationExecutor.ServiceStep
    {
        public override string DisplayText => "Applying container defaults...";

        public override Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            if (SkipWithoutProject(output, service, out var project))
            {
                return Task.CompletedTask;
            }

            if (SkipWithoutContainerInfo(output, service, out var container))
            {
                return Task.CompletedTask;
            }

            DockerfileGenerator.ApplyContainerDefaults(application, project, container);
            return Task.CompletedTask;
        }
    }
}
