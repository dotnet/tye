// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.Tye.Hosting
{
    public class ProcessRunnerOptions
    {
        public const string AllServices = "*";

        public bool BuildProjects { get; set; }

        public bool DebugMode { get; set; }
        public string[]? ServicesToDebug { get; set; }
        public bool DebugAllServices { get; set; }
        public bool ShouldDebugService (string serviceName) => DebugMode && (DebugAllServices || ServicesToDebug!.Contains(serviceName, StringComparer.OrdinalIgnoreCase));

        public bool WatchMode { get; set; }
        public string[]? ServicesToWatch { get; set; }
        public bool WatchAllServices { get; set; }
        public bool ShouldWatchService(string serviceName) => WatchMode && (WatchAllServices || ServicesToWatch!.Contains(serviceName, StringComparer.OrdinalIgnoreCase));

        public static ProcessRunnerOptions FromHostOptions(HostOptions options)
        {
            return new ProcessRunnerOptions
            {
                BuildProjects = !options.NoBuild,
                
                DebugMode = options.Debug.Any(),
                ServicesToDebug = options.Debug.ToArray(),
                DebugAllServices = options.Debug?.Contains(AllServices, StringComparer.OrdinalIgnoreCase) ?? false,
                
                WatchMode = options.Watch.Any(),
                ServicesToWatch = options.Watch.ToArray(),
                WatchAllServices = options.Watch?.Contains(AllServices, StringComparer.OrdinalIgnoreCase) ?? false,
            };
        }
    }
}
