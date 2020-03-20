// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model
{
    public class ServiceJson
    {
        public ServiceDescriptionJson? Description { get; set; }
        public ServiceType ServiceType { get; set; }
        public int Restarts { get; set; }
        public ServiceStatus? Status { get; set; }
        public Dictionary<string, ReplicaStatusJson>? Replicas { get; set; }
    }
}
