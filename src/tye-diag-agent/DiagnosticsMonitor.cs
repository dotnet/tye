// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Tye.Hosting.Diagnostics;

namespace Microsoft.Tye
{
    public class DiagnosticsMonitor : BackgroundService
    {
        private readonly ILogger<DiagnosticsMonitor> logger;
        private readonly IOptions<DiagnosticsMonitorOptions> options;

        public DiagnosticsMonitor(ILogger<DiagnosticsMonitor> logger, IOptions<DiagnosticsMonitorOptions> options)
        {
            this.logger = logger;
            this.options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var collector = InitializeCollector();

            var replicaInfo = new ReplicaInfo(
                selector: processes =>
                {
                    // Find process by looking for the entry point dll in the arguments.
                    return processes.FirstOrDefault(p =>
                    {
                        logger.LogDebug("Checking process {PID}.", p.Id);

                        var command = GetCommand(p);
                        logger.LogDebug("Searching command '{Command}'.", command);

                        return command.Contains($"{options.Value.AssemblyName}.dll");
                    });
                },
                assemblyName: options.Value.AssemblyName,
                service: options.Value.Service,
                replica: options.Value.Kubernetes ? Dns.GetHostName() : options.Value.Service,
                metrics: new ConcurrentDictionary<string, string>());

            while (!stoppingToken.IsCancellationRequested)
            {
                // The collector has a timeout in waiting for its filter to pass, so we
                // won't burn the CPU to a crisp by doing this repetatively.
                try
                {
                    logger.LogInformation("Starting data collection");
                    await collector.CollectAsync(replicaInfo, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Data collection threw an exception.");
                }
            }
        }

        private DiagnosticsCollector InitializeCollector()
        {
            var collector = new DiagnosticsCollector(this.logger)
            {
                SelectProcessTimeout = TimeSpan.FromSeconds(60),
            };

            foreach (var provider in options.Value.Providers)
            {
                if (!DiagnosticsProvider.WellKnownProviders.TryGetValue(provider.Key, out var wellKnown))
                {
                    logger.LogError("Unknown provider type {Provider}. Skipping.", provider.Value);
                    continue;
                }

                switch (wellKnown.Kind)
                {
                    case DiagnosticsProvider.ProviderKind.Logging:
                        {
                            if (collector.LoggingSink is object)
                            {
                                logger.LogError("Logging is already initialized. Skipping.");
                                continue;
                            }

                            logger.LogInformation(wellKnown.LogFormat, provider.Key);
                            collector.LoggingSink = new LoggingSink(logger, provider);
                            break;
                        }

                    case DiagnosticsProvider.ProviderKind.Metrics:
                        {
                            if (collector.MetricSink is object)
                            {
                                logger.LogError("Metrics is already initialized. Skipping.");
                                continue;
                            }

                            // TODO metrics
                            break;
                        }

                    case DiagnosticsProvider.ProviderKind.Tracing:
                        {
                            if (collector.TracingSink is object)
                            {
                                logger.LogError("Tracing is already initialized. Skipping.");
                                continue;
                            }

                            logger.LogInformation(wellKnown.LogFormat, provider.Value);
                            collector.TracingSink = new TracingSink(logger, provider);
                            break;
                        }

                    default:
                        logger.LogError("Unknown provider type. Skipping.");
                        break;
                }
            }

            return collector;
        }

        private static string GetCommand(Process target)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException();
            }

            using var process = new Process()
            {
                StartInfo =
                {
                    FileName = "ps",
                    Arguments = $"-p {target.Id.ToString(CultureInfo.InvariantCulture)} -o command=",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };
            process.Start();
            process.WaitForExit();

            return process.StandardOutput.ReadToEnd().Trim();
        }
    }
}
