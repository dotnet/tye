// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model
{
    public class ProjectRunInfo : RunInfo
    {
        public ProjectRunInfo(string project, string? args, bool build)
        {
            Project = project;
            Args = args;
            Build = build;
        }

        public string? Args { get; }
        public bool Build { get; }
        public string Project { get; }

        // This exists for running projects as containers
        public Dictionary<string, string> VolumeMappings { get; } = new Dictionary<string, string>();
    }
}
