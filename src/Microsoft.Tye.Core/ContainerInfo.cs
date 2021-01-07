// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Tye;

namespace Microsoft.Tye
{
    public sealed class ContainerInfo
    {
        /// <summary>
        /// Gets or sets the base image. If null, the base image will be chosen
        /// based on the project configuration.
        /// </summary>
        public ImageInfo BaseImage { get; set; } = new ImageInfo();

        /// <summary>
        /// Gets or sets the build image. If null, the base image will be chosen
        /// based on the project configuration.
        /// </summary>
        public ImageInfo BuildImage { get; set; } = new ImageInfo();

        /// <summary>
        /// Gets or sets the image. If null, the base image will be chosen
        /// based on the project configuration.
        /// </summary>
        public ImageInfo Image { get; set; } = new ImageInfo();
        
        /// <summary>
        /// Gets or a sets value which determines whether a multi-phase Dockerfile is used.
        /// </summary>
        public bool? UseMultiphaseDockerfile { get; set; }
    }
}
