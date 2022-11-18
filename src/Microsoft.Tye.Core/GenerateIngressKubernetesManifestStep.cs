// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class GenerateIngressKubernetesManifestStep : ApplicationExecutor.IngressStep
    {
        public override string DisplayText => "Generating Manifests...";

        public string Environment { get; set; } = "production";

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application, IngressBuilder ingress)
        {
            ingress.Outputs.Add(await KubernetesManifestGenerator.CreateIngress(output, application, ingress, Environment));
        }
    }
}
