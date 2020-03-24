// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class GenerateKubernetesManifestStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Generating Manifests...";

        public string Environment { get; set; } = "production";


        public override Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            if (SkipWithoutContainerOutput(output, service))
            {
                return Task.CompletedTask;
            }

            if (SkipWithoutProject(output, service, out var project))
            {
                return Task.CompletedTask;
            }

            service.Outputs.Add(KubernetesManifestGenerator.CreateDeployment(output, application, project));

            if (service.Bindings.Count > 0)
            {
                service.Outputs.Add(KubernetesManifestGenerator.CreateService(output, application, project));
            }

            return Task.CompletedTask;
        }
    }
}
