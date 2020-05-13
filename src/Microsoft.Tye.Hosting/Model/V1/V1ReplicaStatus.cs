// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model.V1
{
    public class V1ReplicaStatus
    {
        public string? DockerCommand { get; set; }
        public string? ContainerId { get; set; }
        public string? DockerNetwork { get; set; }
        public string? DockerNetworkAlias { get; set; }
        public string? Name { get; set; }
        public IEnumerable<int>? Ports { get; set; }
        public int? ExitCode { get; set; }
        public int? Pid { get; set; }
        public IDictionary<string, string>? Environment { get; set; }
        public ReplicaState? State { get; set; }
    }
}
