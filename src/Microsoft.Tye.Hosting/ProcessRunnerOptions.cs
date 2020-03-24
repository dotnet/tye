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
        public string[]? ServicesToDebug { get; set; }
        public bool DebugAllServices { get; set; }

        public static ProcessRunnerOptions FromArgs(string[] args, string[] servicesToDebug)
        {
            return new ProcessRunnerOptions
            {
                BuildProjects = !args.Contains("--no-build"),
                DebugMode = args.Contains("--debug"),
                ServicesToDebug = servicesToDebug,
                DebugAllServices = servicesToDebug?.Contains("*", StringComparer.OrdinalIgnoreCase) ?? false
            };
        }
    }
}
