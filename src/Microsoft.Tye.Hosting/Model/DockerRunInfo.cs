// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model
{
    public class DockerRunInfo : RunInfo
    {
        public DockerRunInfo(string image, string? args)
        {
            Image = image;
            Args = args;
        }

        public string? WorkingDirectory { get; set; }

        public Dictionary<string, string> VolumeMappings { get; } = new Dictionary<string, string>();

        public string? Args { get; }

        public string Image { get; }
    }
}
