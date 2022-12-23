// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Tye
{
    public class DotnetProjectServiceBuilder : ProjectServiceBuilder
    {
        public DotnetProjectServiceBuilder(string name, FileInfo projectFile, ServiceSource source)
            : base(name, source)
        {
            ProjectFile = projectFile;
        }

        public FileInfo ProjectFile { get; }
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
        public Dictionary<string, string> BuildProperties { get; } = new Dictionary<string, string>();
        public bool HotReload { get; set; }
    }
}
