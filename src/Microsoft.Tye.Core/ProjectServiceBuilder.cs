// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
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

        // This is always set on the ApplicationFactory codepath.
        public string TargetFramework { get; set; } = default!;

        // This is always set on the ApplicationFactory codepath.
        public string[] TargetFrameworks { get; set; } = default!;

        // This is always set on the ApplicationFactory codepath.
        public string Version { get; set; } = default!;

        // This is always set on the ApplicationFactory codepath.
        public string TargetPath { get; set; } = default!;

        // This is always set on the ApplicationFactory codepath.
        public string RunCommand { get; set; } = default!;

        // This is always set on the ApplicationFactory codepath.
        public string RunArguments { get; set; } = default!;

        // This is always set on the ApplicationFactory codepath.
        public string AssemblyName { get; set; } = default!;

        // This is always set on the ApplicationFactory codepath.
        public string PublishDir { get; set; } = default!;

        // This is always set on the ApplicationFactory codepath.
        public string IntermediateOutputPath { get; set; } = default!;

        // Data used for building containers
        public ContainerInfo? ContainerInfo { get; set; }

        public List<EnvironmentVariable> EnvironmentVariables { get; } = new List<EnvironmentVariable>();

        // Used when running in a container locally.
        public List<VolumeBuilder> Volumes { get; } = new List<VolumeBuilder>();
    }
}
