// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace E2ETest
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    internal class SkipOnLinuxAttribute : Attribute, ITestCondition
    {
        public SkipOnLinuxAttribute()
        {
            IsMet = !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            SkipReason = "This is linux. Here's a free bus-ticket to skipsville.";
        }

        public bool IsMet { get; }

        public string SkipReason { get; set; }
    }
}
