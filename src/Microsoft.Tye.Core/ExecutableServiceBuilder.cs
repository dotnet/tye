// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public sealed class ExecutableServiceBuilder : ServiceBuilder
    {
        public ExecutableServiceBuilder(string name, string executable)
            : base(name)
        {
            Executable = executable;
        }

        public string Executable { get; set; }

        public string? WorkingDirectory { get; set; }

        public string? Args { get; set; }

        public int Replicas { get; set; } = 1;

        public List<EnvironmentVariable> EnvironmentVariables { get; } = new List<EnvironmentVariable>();
    }
}
