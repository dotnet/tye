// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Tye.Hosting.Model
{
    public class Probe
    {
        public HttpProber? Http { get; set; }
        public TimeSpan InitialDelay { get; set; }
        public TimeSpan Period { get; set; }
        public TimeSpan Timeout { get; set; }
        public int SuccessThreshold { get; set; }
        public int FailureThreshold { get; set; }
    }
}
