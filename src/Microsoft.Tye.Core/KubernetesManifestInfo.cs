// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public sealed class KubernetesManifestInfo
    {
        public KubernetesManifestInfo()
        {
            // Create deployment and service by default
            Deployment = new DeploymentManifestInfo();
            Service = new ServiceManifestInfo();
        }

        public DeploymentManifestInfo? Deployment { get; }

        public ServiceManifestInfo Service { get; }
    }
}
