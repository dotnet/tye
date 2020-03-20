// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.Tye.Hosting
{
    public class ProcessRunnerOptions
    {
        public bool DebugMode { get; set; }
        public bool BuildProjects { get; set; }
        public string[]? ProjectsToDebug { get; set; }
        public bool DebugAllProjects { get; set; }

        public static ProcessRunnerOptions FromArgs(string[] args, string projectsToDebug)
        {
            var projectsToDebugArray = projectsToDebug.Split(",", StringSplitOptions.RemoveEmptyEntries);

            return new ProcessRunnerOptions
            {
                BuildProjects = !args.Contains("--no-build"),
                DebugMode = args.Contains("--debug"),
                ProjectsToDebug = projectsToDebugArray,
                DebugAllProjects = projectsToDebugArray.Contains("all", StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}
