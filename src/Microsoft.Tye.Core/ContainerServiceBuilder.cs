// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public sealed class ContainerServiceBuilder : ServiceBuilder
    {
        public ContainerServiceBuilder(string name, string image, ServiceSource source)
            : base(name, source)
        {
            Image = image;
        }

        public string Image { get; }

        public bool IsAspNet { get; set; }

        public string? Args { get; set; }

        public int Replicas { get; set; } = 1;

        public List<EnvironmentVariableBuilder> EnvironmentVariables { get; } = new List<EnvironmentVariableBuilder>();

        public List<VolumeBuilder> Volumes { get; } = new List<VolumeBuilder>();

        public ProbeBuilder? Liveness { get; set; }

        public ProbeBuilder? Readiness { get; set; }
    }
}
