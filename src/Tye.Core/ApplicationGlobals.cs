// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Tye
{
    public sealed class ApplicationGlobals
    {
        public DeploymentKind DeploymentKind { get; set; } = DeploymentKind.Kubernetes;

        public string? Name { get; set; }

        public ContainerRegistry? Registry { get; set; }
    }
}
