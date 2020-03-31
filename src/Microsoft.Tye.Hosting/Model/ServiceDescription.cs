// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model
{
    public class ServiceDescription
    {
        public ServiceDescription(string name, RunInfo? runInfo)
        {
            Name = name;
            RunInfo = runInfo;
        }

        public string Name { get; }
        public RunInfo? RunInfo { get; set; }
        public int Replicas { get; set; } = 1;
        public List<ServiceBinding> Bindings { get; } = new List<ServiceBinding>();
        public List<EnvironmentVariable> Configuration { get; } = new List<EnvironmentVariable>();
    }
}
