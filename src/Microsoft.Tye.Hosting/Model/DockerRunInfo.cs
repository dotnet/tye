// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public bool Private { get; set; }

        public string? NetworkAlias { get; set; }

        public string? WorkingDirectory { get; set; }

        public List<DockerVolume> VolumeMappings { get; } = new List<DockerVolume>();

        public string? Args { get; }

        public string Image { get; }

        public FileInfo? DockerFile { get; set; }
    }
}
