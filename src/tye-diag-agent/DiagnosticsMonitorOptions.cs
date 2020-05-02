// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public class DiagnosticsMonitorOptions
    {
        public bool Kubernetes { get; set; }

        public string Service { get; set; } = default!;

        public string AssemblyName { get; set; } = default!;

        public List<DiagnosticsProvider> Providers { get; } = new List<DiagnosticsProvider>();
    }
}
