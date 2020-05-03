// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Diagnostics.Metrics;

namespace Microsoft.Tye.Hosting.Diagnostics
{
    public class MetricSink
    {
        private readonly ILogger _logger;

        public MetricSink(ILogger logger)
        {
            _logger = logger;
        }

        public IDisposable Attach(EventPipeEventSource source, ReplicaInfo replicaInfo)
        {
            var store = replicaInfo.Metrics;

            source.Dynamic.All += traceEvent =>
            {
                try
                {
                    // Metrics
                    if (traceEvent.EventName.Equals("EventCounters"))
                    {
                        var value = (IDictionary<string, object>)traceEvent.PayloadValue(0);
                        var eventPayload = (IDictionary<string, object>)value["Payload"];

                        var payload = CounterPayload.FromPayload(eventPayload);

                        store[traceEvent.ProviderName + "/" + payload.Name] = payload.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing counter for {ProviderName}:{EventName}", traceEvent.ProviderName, traceEvent.EventName);
                }
            };

            return new NullDisposable();
        }

        private class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
