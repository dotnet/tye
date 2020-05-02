// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public class HostOptions
    {
        public bool Dashboard { get; set; }

        public List<string> Debug { get; } = new List<string>();

        public (string Key, string Value) DistributedTraceProvider { get; set; }

        public bool Docker { get; set; }

        public (string Key, string Value) LoggingProvider { get; set; }

        public (string Key, string Value) MetricsProvider { get; set; }

        public bool NoBuild { get; set; }

        public int? Port { get; set; }
    }
}
