// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tye.Hosting.Diagnostics
{
    public class DiagnosticOptions
    {
        public (string Key, string Value) LoggingProvider { get; set; }
        public (string Key, string Value) DistributedTraceProvider { get; set; }
        public (string Key, string Value) MetricsProvider { get; set; }

        public static DiagnosticOptions FromConfiguration(IConfiguration configuration)
        {
            return new DiagnosticOptions
            {
                LoggingProvider = GetProvider(configuration, "logs"),
                DistributedTraceProvider = GetProvider(configuration, "dtrace"),
                MetricsProvider = GetProvider(configuration, "metrics")
            };
        }

        private static (string, string) GetProvider(IConfiguration configuration, string providerName)
        {
            var providerString = configuration[providerName];

            if (string.IsNullOrEmpty(providerString))
            {
                return (null, null);
            }

            var pair = providerString.Split('=');

            if (pair.Length < 2)
            {
                return (pair[0].Trim(), null);
            }

            return (pair[0].Trim(), pair[1].Trim());
        }

        public void DumpDiagnostics(ILogger logger)
        {
            var (logProviderKey, logProviderValue) = LoggingProvider;
            var (dTraceProviderKey, dTraceProviderValue) = DistributedTraceProvider;

            switch (logProviderKey?.ToLowerInvariant())
            {
                case "elastic":
                    logger.LogInformation("logs: Using ElasticSearch at {URL}", logProviderValue);
                    break;
                case "ai":
                    logger.LogInformation("logs: Using ApplicationInsights instrumentation key {InstrumentationKey}", logProviderValue);
                    break;
                case "console":
                    logger.LogInformation("logs: Using console logs");
                    break;
                case "seq":
                    logger.LogInformation("logs: Using Seq at {URL}", logProviderValue);
                    break;
                default:
                    break;
            }

            switch (dTraceProviderKey?.ToLowerInvariant())
            {
                case "zipkin":
                    logger.LogInformation("dtrace: Using Zipkin at URL {URL}", dTraceProviderValue);
                    break;
                default:
                    break;
            }
        }
    }
}
