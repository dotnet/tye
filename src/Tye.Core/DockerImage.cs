// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Tye
{
    public sealed class DockerImage : Source
    {
        public DockerImage(string image)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            Image = image;
        }

        public string Image { get; }
    }
}
