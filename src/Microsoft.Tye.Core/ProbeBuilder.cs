// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public class ProbeBuilder
    {
        public HttpProberBuilder? Http { get; set; }
        public int InitialDelay { get; set; }
        public int Period { get; set; }
        public int Timeout { get; set; }
        public int SuccessThreshold { get; set; }
        public int FailureThreshold { get; set; }
    }
}
