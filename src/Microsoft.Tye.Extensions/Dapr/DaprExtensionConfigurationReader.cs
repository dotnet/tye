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
                var services = new Dictionary<string, DaprExtensionServiceConfiguration>(StringComparer.OrdinalIgnoreCase);

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

            serviceConfiguration.AppId = TryGetValue(rawConfiguration, "app-id");
            serviceConfiguration.Enabled = TryGetValue<bool>(rawConfiguration, "enabled");
            serviceConfiguration.GrpcPort = TryGetValue<int>(rawConfiguration, "grpc-port");
            serviceConfiguration.HttpPort = TryGetValue<int>(rawConfiguration, "http-port");
            serviceConfiguration.MetricsPort = TryGetValue<int>(rawConfiguration, "metrics-port");
            serviceConfiguration.ProfilePort = TryGetValue<int>(rawConfiguration, "profile-port");
        }

        private static void ReadCommonConfiguration(IDictionary<string, object> rawConfiguration, DaprExtensionCommonConfiguration commonConfiguration)
        {
            commonConfiguration.AppMaxConcurrency = TryGetValue<int>(rawConfiguration, "app-max-concurrency");
            commonConfiguration.AppProtocol = TryGetValue(rawConfiguration, "app-protocol");
            commonConfiguration.AppSsl = TryGetValue<bool>(rawConfiguration, "app-ssl");
            commonConfiguration.ComponentsPath = TryGetValue(rawConfiguration, "components-path");
            commonConfiguration.Config = TryGetValue(rawConfiguration, "config");
            commonConfiguration.EnableProfiling = TryGetValue<bool>(rawConfiguration, "enable-profiling");
            commonConfiguration.HttpMaxRequestSize = TryGetValue<int>(rawConfiguration, "http-max-request-size");
            commonConfiguration.LogLevel = TryGetValue(rawConfiguration, "log-level");
            commonConfiguration.PlacementPort = TryGetValue<int>(rawConfiguration, "placement-port");
        }

        private static string? TryGetValue(IDictionary<string, object> rawConfiguration, string name)
        {
            return rawConfiguration.TryGetValue(name, out var obj) && obj?.ToString() is string
                ? obj.ToString()
                : null;
        }

        private static T? TryGetValue<T>(IDictionary<string, object> rawConfiguration, string name)
            where T : struct
        {
            if (rawConfiguration.TryGetValue(name, out var obj) && obj?.ToString() is string)
            {
                try
                {
                    return (T?)Convert.ChangeType(obj.ToString(), typeof(T));
                }
                catch
                {
                    // No-op.
                }
            }

            return null;
        }
    }
}
