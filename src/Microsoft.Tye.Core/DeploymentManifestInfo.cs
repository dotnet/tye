// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public sealed class DeploymentManifestInfo
    {
        public Dictionary<string, string> Annotations { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> Labels { get; } = new Dictionary<string, string>();
    }
}
