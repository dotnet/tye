// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Tye.Hosting.Diagnostics
{
    public class ReplicaInfo
    {
        public ReplicaInfo(
            Func<IReadOnlyList<Process>, Process?> selector,
            string assemblyName,
            string service,
            string replica,
            ConcurrentDictionary<string, string> metrics)
        {
            Selector = selector;
            AssemblyName = assemblyName;
            Service = service;
            Replica = replica;
            Metrics = metrics;
        }

        public Func<IReadOnlyList<Process>, Process?> Selector { get; }

        public string AssemblyName { get; }

        public string Service { get; }

        public string Replica { get; }

        // TODO - this isn't a great way to pass metrics around.
        public ConcurrentDictionary<string, string> Metrics { get; }
    }
}
