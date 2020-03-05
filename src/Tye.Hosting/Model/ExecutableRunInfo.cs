// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Tye.Hosting.Model
{
    public class ExecutableRunInfo : RunInfo
    {
        public ExecutableRunInfo(string executable, string? workingDirectory, string? args)
        {
            Executable = executable;
            WorkingDirectory = workingDirectory;
            Args = args;
        }

        public string Executable { get; }

        public string? WorkingDirectory { get; }

        public string? Args { get; set; }
    }
}
