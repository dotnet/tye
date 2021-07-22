// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public class NodeServiceBuilder : LaunchedServiceBuilder
    {
        public NodeServiceBuilder(string name, string packagePath, ServiceSource source)
            : base(name, source)
        {
            PackagePath = packagePath;
        }

        public bool? EnableDebugging { get; set; }

        public string PackagePath { get; set; }

        public string? Script { get; set; }
    }
}
