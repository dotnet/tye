// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Tye;

namespace E2ETest
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    internal class SkipIfNoDockerAttribute : Attribute
    {
        public SkipIfNoDockerAttribute()
        {
            IsMet = DockerDetector.Instance.;
            SkipReason = "Docker is not installed or running.";
        }

        public bool IsMet { get; }

        public string SkipReason { get; set; }

    }
}
