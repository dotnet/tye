﻿namespace Opulence
{
    public sealed class ContainerInfo
    {
        /// <summary>
        /// Gets or sets the name of the base image. If null, the base image will be chosen
        /// based on the project configuration.
        /// </summary>
        public string? BaseImageName { get; set; }

        /// <summary>
        /// Gets or sets the name of the base image tag. If null, the base image tag will be chosen
        /// based on the project configuration.
        /// </summary>
        public string? BaseImageTag { get; set; }

        /// <summary>
        /// Gets or sets the name of the build image. If null, the build image will be chosen
        /// based on the project configuration.
        /// </summary>
        public string? BuildImageName { get; set; }

        /// <summary>
        /// Gets or sets the name of the base image tag. If null, the build image tag will be chosen
        /// based on the project configuration.
        /// </summary>
        public string? BuildImageTag { get; set; }

        /// <summary>
        /// Gets or sets the name of the image. If null, the image name will be chosen
        /// based on the project name.
        /// </summary>
        public string? ImageName { get; set; }

        /// <summary>
        /// Gets or sets the tag of the image. If null, the image tag will be chosen
        /// based on the project version.
        /// </summary>
        public string? ImageTag { get; set; }

        /// <summary>
        /// Gets or a sets value which determines whether a multiphase dockerfile is used.
        /// </summary>
        public bool? UseMultiphaseDockerfile { get; set; }
    }
}
