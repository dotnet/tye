// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Tye;

namespace Microsoft.Tye
{
    public sealed class ContainerInfo
    {
        public ImageInfo BaseImage { get; set; } = new ImageInfo();
        public ImageInfo BuildImage { get; set; } = new ImageInfo();
        public ImageInfo Image { get; set; } = new ImageInfo();

        /// <summary>
        /// Gets or sets the tag of the image. If null, the image tag will be chosen
        /// based on the project version.
        /// </summary>
        public string? ImageTag { get; set; }

        /// <summary>
        /// Gets or a sets value which determines whether a multi-phase Dockerfile is used.
        /// </summary>
        public bool? UseMultiphaseDockerfile { get; set; }
    }
}
