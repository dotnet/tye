// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public class DockerFileServiceBuilder : ProjectServiceBuilder
    {
        public DockerFileServiceBuilder(string name, string image, ServiceSource source)
            : base(name, source)
        {
            Image = image;
        }
        public string Image { get; }

        public string? DockerFile { get; set; }
        public Dictionary<string, string> BuildArgs { get; set; } = new Dictionary<string, string>();

        public string? DockerFileContext { get; set; }
    }
}
