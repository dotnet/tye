// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Extensions.Dapr
{
    internal abstract class DaprExtensionCommonConfiguration
    {
        public int? AppMaxConcurrency { get; set; }
        public string? AppProtocol { get; set; }
        public bool? AppSsl { get; set; }
        public string? ComponentsPath { get; set;}
        public string? Config { get; set; }
        public bool? EnableProfiling { get; set; }
        public int? HttpMaxRequestSize { get; set; }
        public string? LogLevel { get; set; }
        public int? PlacementPort { get; set; }
    }

    internal sealed class DaprExtensionServiceConfiguration : DaprExtensionCommonConfiguration
    {
        public string? AppId { get; set; }
        public bool? Enabled { get; set; }
        public int? GrpcPort { get; set; }
        public int? HttpPort { get; set; }
        public int? MetricsPort { get; set; }
        public int? ProfilePort { get; set; }
    }

    internal sealed class DaprExtensionConfiguration : DaprExtensionCommonConfiguration
    {
        public IReadOnlyDictionary<string, DaprExtensionServiceConfiguration> Services { get; set; }
            = new Dictionary<string, DaprExtensionServiceConfiguration>();
    }
}
