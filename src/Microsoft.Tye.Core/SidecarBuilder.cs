// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public class SidecarBuilder
    {
        public SidecarBuilder(string name, string imageName, string imageTag)
        {
            Name = name;
            ImageName = imageName;
            ImageTag = imageTag;
        }

        public string Name { get; }

        public string ImageName { get; }

        public string ImageTag { get; }

        public List<string> Args { get; } = new List<string>();

        public HashSet<string> Dependencies { get; } = new HashSet<string>();

        public List<ServiceOutput> Outputs { get; } = new List<ServiceOutput>();

        public List<EnvironmentVariableBuilder> EnvironmentVariables { get; } = new List<EnvironmentVariableBuilder>();
    }
}
