// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model.V1
{
    public class V1Service
    {
        public V1ServiceDescription? Description { get; set; }
        public ServiceType ServiceType { get; set; }
        public int Restarts { get; set; }
        public V1ServiceStatus? Status { get; set; }
        public Dictionary<string, V1ReplicaStatus>? Replicas { get; set; }
    }
}
