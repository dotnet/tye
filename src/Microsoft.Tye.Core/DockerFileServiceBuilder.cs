// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public class DockerFileServiceBuilder : ProjectServiceBuilder
    {
        public DockerFileServiceBuilder(string name, string image)
            : base(name)
        {
            Image = image;
        }
        public string Image { get; set; }

        public string? DockerFile { get; set; }

        public string? DockerFileContext { get; set; }
    }
}
