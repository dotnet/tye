// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye.Hosting.Model
{
    public class NodeRunInfo : ExecutableRunInfo
    {
        public NodeRunInfo(string executable, string? workingDirectory, string? args, bool enableDebugging)
            : base(executable, workingDirectory, args)
        {
            EnableDebugging = enableDebugging;
        }

        public bool EnableDebugging { get; }
    }
}
