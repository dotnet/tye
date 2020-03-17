// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Tye;

namespace E2ETest
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    internal class SkipIfDockerNotRunningAttribute : Attribute, ITestCondition
    {
        public SkipIfDockerNotRunningAttribute()
        {
            // TODO Check performance of this.
            IsMet = DockerDetector.Instance.IsDockerConnectedToDaemon.Value.GetAwaiter().GetResult();
            SkipReason = "Docker is not installed or running.";
        }

        public bool IsMet { get; }

        public string SkipReason { get; set; }
    }
}
