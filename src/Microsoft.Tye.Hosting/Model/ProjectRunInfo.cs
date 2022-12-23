// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Tye.Hosting.Model
{
    public class ProjectRunInfo : RunInfo
    {
        public ProjectRunInfo(DotnetProjectServiceBuilder project)
        {
            ProjectFile = project.ProjectFile;
            Args = project.Args;
            Build = project.Build;
            HotReload = project.HotReload;
            BuildProperties = project.BuildProperties;
            TargetFramework = project.TargetFramework;
            TargetFrameworkName = project.TargetFrameworkName;
            TargetFrameworkVersion = project.TargetFrameworkVersion;
            Version = project.Version;
            AssemblyName = project.AssemblyName;
            TargetAssemblyPath = project.TargetPath;
            RunCommand = project.RunCommand;
            RunArguments = project.RunArguments;
            PublishOutputPath = project.PublishDir;
            ContainerBaseImage = project.ContainerInfo?.BaseImageName;
            ContainerBaseTag = project.ContainerInfo?.BaseImageTag;
            IsAspNet = project.IsAspNet;
        }

        public Dictionary<string, string> BuildProperties { get; } = new Dictionary<string, string>();

        public string? Args { get; }
        public bool Build { get; set; }
        public bool HotReload { get; set; }
        public FileInfo ProjectFile { get; }
        public string TargetFrameworkName { get; set; } = default!;
        public string TargetFrameworkVersion { get; set; } = default!;
        public string TargetFramework { get; }
        public bool IsAspNet { get; }
        public string Version { get; }

        public string AssemblyName { get; }

        public string TargetAssemblyPath { get; }

        public string PublishOutputPath { get; }

        public string RunCommand { get; }
        public string RunArguments { get; }

        public string? ContainerBaseTag { get; }
        public string? ContainerBaseImage { get; }

        // This exists for running projects as containers
        public List<DockerVolume> VolumeMappings { get; } = new List<DockerVolume>();
    }
}
