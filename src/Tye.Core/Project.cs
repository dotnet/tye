// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Tye
{
    public sealed class Project : Source
    {
        public Project(string relativeFilePath)
        {
            if (relativeFilePath is null)
            {
                throw new ArgumentNullException(nameof(relativeFilePath));
            }

            RelativeFilePath = relativeFilePath;
        }

        public string RelativeFilePath { get; }

        public FrameworkCollection Frameworks { get; } = new FrameworkCollection();

        public string TargetFramework { get; set; } = default!;

        public string Version { get; set; } = default!;
    }
}
