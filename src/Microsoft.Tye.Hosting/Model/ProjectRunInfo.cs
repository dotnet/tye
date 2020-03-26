// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Tye.Hosting.Model
{
    public class ProjectRunInfo : RunInfo
    {
        public ProjectRunInfo(ProjectServiceBuilder project)
        {
            ProjectFile = project.ProjectFile;
            Args = project.Args;
            Build = project.Build;
            TargetFramework = project.TargetFramework;
            Version = project.Version;
            AssemblyName = project.AssemblyName;
            TargetAssemblyPath = project.TargetPath;
            RunCommand = project.RunCommand;
            RunArguments = project.RunArguments;
            PublishOutputPath = project.PublishDir;
        }

        public string? Args { get; }
        public bool Build { get; }
        public FileInfo ProjectFile { get; }

        public string TargetFramework { get; }

        public string Version { get; }

        public string AssemblyName { get; }

        public string TargetAssemblyPath { get; }

        public string PublishOutputPath { get; }

        public string RunCommand { get; }
        public string RunArguments { get; }

        // This exists for running projects as containers
        public Dictionary<string, string> VolumeMappings { get; } = new Dictionary<string, string>();
    }
}
