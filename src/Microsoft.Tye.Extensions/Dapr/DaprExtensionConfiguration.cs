// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Extensions.Dapr
{
    internal abstract class DaprExtensionCommonConfiguration
    {
        public int? PlacementPort  { get; set; }
    }

    internal sealed class DaprExtensionServiceConfiguration : DaprExtensionCommonConfiguration
    {
        public bool? Enabled { get; set; }
    }

    internal sealed class DaprExtensionConfiguration : DaprExtensionCommonConfiguration
    {
        public IReadOnlyDictionary<string, DaprExtensionServiceConfiguration> Services { get; set; }
            = new Dictionary<string, DaprExtensionServiceConfiguration>();
    }
}