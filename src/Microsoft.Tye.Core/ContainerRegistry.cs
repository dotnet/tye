// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Tye
{
    public sealed class ContainerRegistry
    {
        public ContainerRegistry(string hostname)
        {
            Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        }

        public string Hostname { get; }
    }
}
