// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public sealed class ExecutableServiceBuilder : LaunchedServiceBuilder
    {
        public ExecutableServiceBuilder(string name, string executable, ServiceSource source)
            : base(name, source)
        {
            Executable = executable;
        }

        public string Executable { get; }

        public string? WorkingDirectory { get; set; }

        public string? Args { get; set; }

        public ProbeBuilder? Liveness { get; set; }

        public ProbeBuilder? Readiness { get; set; }
    }
}
