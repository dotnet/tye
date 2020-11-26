// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Tye.Hosting.Model
{
    public class DockerRunInfo : RunInfo
    {
        public DockerRunInfo(string image, string? args)
        {
            Image = image;
            Args = args;
        }
        public bool IsProxy { get; set; }
        public bool Private { get; set; }
        public bool IsAspNet { get; set; }

        public string? NetworkAlias { get; set; }

        public string? WorkingDirectory { get; set; }

        public List<DockerVolume> VolumeMappings { get; } = new List<DockerVolume>();

        public string? Args { get; }
        public Dictionary<string, string> BuildArgs { get; set; } = new Dictionary<string, string>();

        public string Image { get; }

        public FileInfo? DockerFile { get; set; }

        public FileInfo? DockerFileContext { get; set; }
    }
}
