// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model.V1
{
    public class V1ServiceMetrics
    {
        public string? Service { get; set; }
        public List<V1Metric>? Metrics { get; set; }
    }
}
