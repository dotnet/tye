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
        public bool Watch { get; set; }
        public bool ManualStartServices { get; set; }
        public string[]? ServicesNotToStart { get; set; }

        public static ProcessRunnerOptions FromHostOptions(HostOptions options)
        {
            return new ProcessRunnerOptions
            {
                BuildProjects = !options.NoBuild,
                DebugMode = options.Debug.Any(),
                ServicesToDebug = options.Debug.ToArray(),
                DebugAllServices = options.Debug?.Contains("*", StringComparer.OrdinalIgnoreCase) ?? false,
                ManualStartServices = options.NoStart?.Contains("*", StringComparer.OrdinalIgnoreCase) ?? false,
                ServicesNotToStart = options.NoStart?.ToArray(),
                Watch = options.Watch
            };
        }
    }
}
