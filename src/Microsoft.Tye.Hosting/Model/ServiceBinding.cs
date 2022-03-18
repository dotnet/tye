// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Net;

namespace Microsoft.Tye.Hosting.Model
{
    public class ServiceBinding
    {
        public string? Name { get; set; }
        public string? ConnectionString { get; set; }
        public int? Port { get; set; }
        public int? ContainerPort { get; set; }
        public string? Host { get; set; }
        public string? IPAddress { get; set; }
        public string? Protocol { get; set; }
        public List<int> ReplicaPorts { get; } = new List<int>();
        public List<string> Routes { get; } = new List<string>();
    }
}
