// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model
{
    public class ReplicaStatus
    {
        public ReplicaStatus(Service service, string name)
        {
            Service = service;
            Name = name;
        }

        public string Name { get; }

        public IEnumerable<int>? Ports { get; set; }

        public Service Service { get; }

        public Dictionary<object, object> Items { get; } = new Dictionary<object, object>();

        public Dictionary<string, string> Metrics { get; set; } = new Dictionary<string, string>();
    }
}
