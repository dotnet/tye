// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Tye
{
    public sealed class ContainerRegistry
    {
        public ContainerRegistry(string hostname, string? pullSecret)
        {
            Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            PullSecret = pullSecret;
        }

        public string Hostname { get; }
        public string? PullSecret { get; }
    }
}
