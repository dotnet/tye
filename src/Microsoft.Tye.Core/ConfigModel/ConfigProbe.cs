// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigProbe
    {
        public ConfigHttpProber? Http { get; set; }
        public int InitialDelay { get; set; } = 0;
        public int Period { get; set; } = 1;
        public int Timeout { get; set; } = 1;
        public int SuccessThreshold { get; set; } = 1;
        public int FailureThreshold { get; set; } = 3;
    }
}
