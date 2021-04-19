// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Tye;

namespace Test.Infrastructure
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public class SkipIfDockerNotRunningAttribute : Attribute, ITestCondition
    {
        public SkipIfDockerNotRunningAttribute()
        {
            // TODO Check performance of this.
            IsMet = DockerDetector.Instance.IsUsable(out string unusableReason) && !(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_OS")));
            SkipReason = $"Container engine not usable: {unusableReason}";
        }

        public bool IsMet { get; }

        public string SkipReason { get; set; }
    }
}
