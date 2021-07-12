// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public class ProjectServiceBuilder : LaunchedServiceBuilder
    {
        public ProjectServiceBuilder(string name)
            : base(name)
        {
        }
        public bool IsAspNet { get; set; }

        public bool Build { get; set; }

        public string? Args { get; set; }

        // Data used for building containers
        public ContainerInfo? ContainerInfo { get; set; }

        // Data used for building Kubernetes manifests
        public KubernetesManifestInfo? ManifestInfo { get; set; }

        // Used when running in a container locally.
        public List<VolumeBuilder> Volumes { get; } = new List<VolumeBuilder>();

        public List<SidecarBuilder> Sidecars { get; } = new List<SidecarBuilder>();

        public ProbeBuilder? Liveness { get; set; }

        public ProbeBuilder? Readiness { get; set; }

        public bool RelocateDiagnosticsDomainSockets { get; set; }
    }
}
