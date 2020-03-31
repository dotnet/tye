// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public sealed class ExtensionConfiguration
    {
        public ExtensionConfiguration(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Dictionary<string, object> Data { get; } = new Dictionary<string, object>();
    }
}
