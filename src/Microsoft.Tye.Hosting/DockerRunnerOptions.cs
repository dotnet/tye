// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.Tye.Hosting
{
    public class DockerRunnerOptions
    {
        public bool ManualStartServices { get; set; }
        public string[]? ServicesNotToStart { get; set; }

        public static DockerRunnerOptions FromHostOptions(HostOptions options)
        {
            return new DockerRunnerOptions
            {
                ManualStartServices = options.NoStart?.Contains("*", StringComparer.OrdinalIgnoreCase) ?? false,
                ServicesNotToStart = options.NoStart?.ToArray()
            };
        }
    }
}
