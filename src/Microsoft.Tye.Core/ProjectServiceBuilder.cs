// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Tye
{
    public sealed class ProjectServiceBuilder : ServiceBuilder
    {
        public ProjectServiceBuilder(string name, FileInfo projectFile)
            : base(name)
        {
            ProjectFile = projectFile;
        }

        public FileInfo ProjectFile { get; }

        public int Replicas { get; set; } = 1;

        public bool Build { get; set; }

        public string? Args { get; set; }

        public FrameworkCollection Frameworks { get; } = new FrameworkCollection();

        // These is always set on the ApplicationFactory codepath.
        public string TargetFrameworkName { get; set; } = default!;
        public string TargetFrameworkVersion { get; set; } = default!;
        public string TargetFramework { get; set; } = default!;
        public string[] TargetFrameworks { get; set; } = default!;
        public string Version { get; set; } = default!;
        public string TargetPath { get; set; } = default!;
        public string RunCommand { get; set; } = default!;
        public string RunArguments { get; set; } = default!;
        public string AssemblyName { get; set; } = default!;
        public string PublishDir { get; set; } = default!;
        public string IntermediateOutputPath { get; set; } = default!;
        public bool IsAspNet { get; set; }
        public bool RelocateDiagnosticsDomainSockets { get; set; }

        // Data used for building containers
        public ContainerInfo? ContainerInfo { get; set; }

        // Data used for building Kubernetes manifests
        public KubernetesManifestInfo? ManifestInfo { get; set; }

        public List<EnvironmentVariableBuilder> EnvironmentVariables { get; } = new List<EnvironmentVariableBuilder>();

        // Used when running in a container locally.
        public List<VolumeBuilder> Volumes { get; } = new List<VolumeBuilder>();

        public Dictionary<string, string> BuildProperties { get; } = new Dictionary<string, string>();

        public List<SidecarBuilder> Sidecars { get; } = new List<SidecarBuilder>();

        public ProbeBuilder? Liveness { get; set; }
        public ProbeBuilder? Readiness { get; set; }
    }
}
