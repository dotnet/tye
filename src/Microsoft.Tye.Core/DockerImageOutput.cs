// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Tye;

namespace Microsoft.Tye
{
    public class DockerImageOutput : ServiceOutput
    {
        public DockerImageOutput(string imageName, string imageTag)
        {
            Info.Name = imageName ?? throw new ArgumentNullException(nameof(imageName));
            Info.Tag = imageTag ?? throw new ArgumentNullException(nameof(imageTag));
        }

        public ImageInfo Info { get; set; } = new ImageInfo();
    }
}
