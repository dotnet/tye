// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model.V1
{
    public class V1RunInfo
    {
        public V1RunInfoType Type { get; set; }
        public string? Args { get; set; }
        public string? Project { get; set; }
        public string? WorkingDirectory { get; set; }
        public List<V1DockerVolume>? VolumeMappings { get; set; }
        public string? Image { get; set; }
        public string? Executable { get; set; }
    }
}
