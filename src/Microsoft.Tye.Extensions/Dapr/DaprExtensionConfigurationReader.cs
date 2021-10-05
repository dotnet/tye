// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Tye.Extensions.Dapr
{
    internal static class DaprExtensionConfigurationReader
    {
        public static DaprExtensionConfiguration ReadConfiguration(IDictionary<string, object> rawConfiguration)
        {
            var configuration = new DaprExtensionConfiguration();

            ReadCommonConfiguration(rawConfiguration, configuration);

            if (rawConfiguration.TryGetValue("services", out var servicesObject) && servicesObject is Dictionary<string, object> rawServicesConfiguration)
            {
                var services = new Dictionary<string, DaprExtensionServiceConfiguration>();

                foreach (var kvp in rawServicesConfiguration)
                {
                    if (kvp.Value is Dictionary<string, object> rawServiceConfiguration)
                    {
                        var serviceConfiguration = new DaprExtensionServiceConfiguration();

                        ReadServiceConfiguration(rawServiceConfiguration, serviceConfiguration);

                        services.Add(kvp.Key, serviceConfiguration);                        
                    }
                }

                configuration.Services = services;
            }

            return configuration;
        }

        private static void ReadServiceConfiguration(IDictionary<string, object> rawConfiguration, DaprExtensionServiceConfiguration serviceConfiguration)
        {
            ReadCommonConfiguration(rawConfiguration, serviceConfiguration);

            if (rawConfiguration.TryGetValue("enabled", out var obj) && obj is string && Boolean.TryParse(obj.ToString(), out var enabled))
            {
                serviceConfiguration.Enabled = enabled;
            }
        }

        private static void ReadCommonConfiguration(IDictionary<string, object> rawConfiguration, DaprExtensionCommonConfiguration commonConfiguration)
        {
            if (rawConfiguration.TryGetValue("placement-port", out var obj) && obj?.ToString() is string && int.TryParse(obj.ToString(), out var customPlacementPort))
            {
                commonConfiguration.PlacementPort = customPlacementPort;
            }
        }
    }
}