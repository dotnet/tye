// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Zipkin;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace Microsoft.Tye.Hosting.Diagnostics
{
    public class TracingSink
    {
        private readonly ILogger _logger;
        private readonly DiagnosticsProvider _provider;

        public TracingSink(ILogger logger, DiagnosticsProvider provider)
        {
            _logger = logger;
            _provider = provider;
        }

        public IDisposable Attach(EventPipeEventSource source, ReplicaInfo replicaInfo)
        {
            var exporter = CreateSpanExporter(replicaInfo);
            if (exporter is null)
            {
                return new NullDisposable();
            }

            var processor = new SimpleSpanProcessor(exporter);
            var activities = new Dictionary<string, ActivityItem>();

            source.Dynamic.All += traceEvent =>
            {

                if (traceEvent.EventName == "Activity1Start/Start")
                {
                    var listenerEventName = (string)traceEvent.PayloadByName("EventName");

                    if (traceEvent.PayloadByName("Arguments") is IDictionary<string, object>[] arguments)
                    {
                        if (TryCreateActivity(arguments, out var item))
                        {
                            string? method = null;
                            string? path = null;
                            string? host = null;
                            string? pathBase = null;
                            string? queryString = null;
                            string? scheme = null;

                            foreach (var arg in arguments)
                            {
                                var key = (string)arg["Key"];
                                var value = (string)arg["Value"];

                                if (key == "Path")
                                {
                                    path = value;
                                }
                                else if (key == "Method")
                                {
                                    method = value;
                                }
                                else if (key == "Host")
                                {
                                    host = value;
                                }
                                else if (key == "PathBase")
                                {
                                    pathBase = value;
                                }
                                else if (key == "Scheme")
                                {
                                    scheme = value;
                                }
                                else if (key == "QueryString")
                                {
                                    queryString = value;
                                }
                            }

                            item.Name = path;
                            item.Kind = SpanKind.Server;
                            item.Attributes[SpanAttributeConstants.HttpUrlKey] = scheme + "://" + host + pathBase + path + queryString;
                            item.Attributes[SpanAttributeConstants.HttpMethodKey] = method;
                            item.Attributes[SpanAttributeConstants.HttpPathKey] = path;

                            activities[item.Id] = item;
                        }
                    }
                }

                if (traceEvent.EventName == "Activity1Stop/Stop")
                {
                    var listenerEventName = (string)traceEvent.PayloadByName("EventName");

                    if (traceEvent.PayloadByName("Arguments") is IDictionary<string, object>[] arguments)
                    {
                        var (activityId, duration) = GetActivityStop(arguments);

                        var statusCode = 0;

                        foreach (var arg in arguments)
                        {
                            var key = (string)arg["Key"];
                            var value = (string)arg["Value"];

                            if (key == "StatusCode")
                            {
                                statusCode = int.Parse(value);
                            }
                        }

                        if (activityId != null && activities.TryGetValue(activityId, out var item))
                        {
                            item.Attributes[SpanAttributeConstants.HttpStatusCodeKey] = statusCode;

                            item.EndTime = item.StartTime + duration;

                            var spanData = new SpanData(item.Name,
                                new SpanContext(item.TraceId, item.SpanId, ActivityTraceFlags.Recorded),
                                item.ParentSpanId,
                                item.Kind,
                                item.StartTime,
                                item.Attributes,
                               Enumerable.Empty<Event>(),
                               Enumerable.Empty<Link>(),
                               null,
                               Status.Ok,
                               item.EndTime);

                            processor.OnEnd(spanData);

                            activities.Remove(activityId);
                        }
                    }
                }
                if (traceEvent.EventName == "Activity2Start/Start")
                {
                    var listenerEventName = (string)traceEvent.PayloadByName("EventName");

                    if (traceEvent.PayloadByName("Arguments") is IDictionary<string, object>[] arguments)
                    {
                        string? uri = null;
                        string? method = null;

                        foreach (var arg in arguments)
                        {
                            var key = (string)arg["Key"];
                            var value = (string)arg["Value"];

                            if (key == "RequestUri")
                            {
                                uri = value;
                            }
                            else if (key == "Method")
                            {
                                method = value;
                            }
                        }

                        if (TryCreateActivity(arguments, out var item))
                        {
                            item.Name = uri;
                            item.Kind = SpanKind.Client;

                            item.Attributes[SpanAttributeConstants.HttpUrlKey] = uri;
                            item.Attributes[SpanAttributeConstants.HttpMethodKey] = method;

                            activities[item.Id] = item;
                        }
                    }
                }
                if (traceEvent.EventName == "Activity2Stop/Stop")
                {
                    var listenerEventName = (string)traceEvent.PayloadByName("EventName");

                    if (traceEvent.PayloadByName("Arguments") is IDictionary<string, object>[] arguments)
                    {
                        var (activityId, duration) = GetActivityStop(arguments);

                        if (activityId != null && activities.TryGetValue(activityId, out var item))
                        {
                            item.EndTime = item.StartTime + duration;

                            var spanData = new SpanData(
                                item.Name,
                                new SpanContext(item.TraceId, item.SpanId, ActivityTraceFlags.Recorded),
                                item.ParentSpanId,
                                item.Kind,
                                item.StartTime,
                                item.Attributes,
                               Enumerable.Empty<Event>(),
                               Enumerable.Empty<Link>(),
                               null,
                               Status.Ok,
                               item.EndTime);

                            processor.OnEnd(spanData);

                            activities.Remove(activityId);
                        }
                    }
                }
            };

            return new NullDisposable();
        }

        private static bool TryCreateActivity(IDictionary<string, object>[] arguments, [MaybeNullWhen(false)] out ActivityItem item)
        {
            string? activityId = null;
            string? operationName = null;
            string? spanId = null;
            string? parentSpanId = null;
            string? traceId = null;
            DateTime startTime = default;
            ActivityIdFormat idFormat = default;

            foreach (var arg in arguments)
            {
                var key = (string)arg["Key"];
                var value = (string)arg["Value"];

                if (key == "ActivityId")
                {
                    activityId = value;
                }
                else if (key == "ActivityOperationName")
                {
                    operationName = value;
                }
                else if (key == "ActivitySpanId")
                {
                    spanId = value;
                }
                else if (key == "ActivityTraceId")
                {
                    traceId = value;
                }
                else if (key == "ActivityParentSpanId")
                {
                    parentSpanId = value;
                }
                else if (key == "ActivityStartTime")
                {
                    startTime = new DateTime(long.Parse(value), DateTimeKind.Utc);
                }
                else if (key == "ActivityIdFormat")
                {
                    idFormat = Enum.Parse<ActivityIdFormat>(value);
                }
            }

            if (string.IsNullOrEmpty(activityId))
            {
                // Not a 3.1 application (we can detect this earlier)
                item = null!;
                return false;
            }

            if (idFormat == ActivityIdFormat.Hierarchical)
            {
                // We need W3C to make it work
                item = null!;
                return false;
            }

            // This is what open telemetry currently does
            // https://github.com/open-telemetry/opentelemetry-dotnet/blob/4ba732af062ddc2759c02aebbc91335aaa3f7173/src/OpenTelemetry.Collector.AspNetCore/Implementation/HttpInListener.cs#L65-L92

            item = new ActivityItem(activityId)
            {
                Name = operationName,
                SpanId = ActivitySpanId.CreateFromString(spanId),
                TraceId = ActivityTraceId.CreateFromString(traceId),
                ParentSpanId = parentSpanId == "0000000000000000" ? default : ActivitySpanId.CreateFromString(parentSpanId),
                StartTime = startTime,
            };

            return true;
        }

        private static (string? ActivityId, TimeSpan Duration) GetActivityStop(IDictionary<string, object>[] arguments)
        {
            var activityId = (string?)null;
            var duration = default(TimeSpan);

            foreach (var arg in arguments)
            {
                var key = (string)arg["Key"];
                var value = (string)arg["Value"];

                if (key == "ActivityId")
                {
                    activityId = value;
                }
                else if (key == "ActivityDuration")
                {
                    duration = new TimeSpan(long.Parse(value));
                }
            }

            return (activityId, duration);
        }

        private SpanExporter? CreateSpanExporter(ReplicaInfo replicaInfo)
        {
            if (string.Equals(_provider.Key, "zipkin", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(_provider.Value))
            {
                var zipkin = new ZipkinTraceExporter(new ZipkinTraceExporterOptions()
                {
                    ServiceName = replicaInfo.Service,
                    Endpoint = new Uri($"{_provider.Value.TrimEnd('/')}/api/v2/spans")
                });

                return zipkin;
            }

            // TODO: Support Jaegar
            // TODO: Support ApplicationInsights
            return null;
        }

        private class ActivityItem
        {
            public ActivityItem(string id)
            {
                Id = id;
            }

            public string Id { get; }
            public string? Name { get; set; }
            public ActivityTraceId TraceId { get; set; }
            public ActivitySpanId SpanId { get; set; }
            public Dictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public SpanKind Kind { get; set; }

            public ActivitySpanId ParentSpanId { get; set; }
        }

        internal static class SpanAttributeConstants
        {
            public static readonly string ComponentKey = "component";

            public static readonly string HttpMethodKey = "http.method";
            public static readonly string HttpStatusCodeKey = "http.status_code";
            public static readonly string HttpUserAgentKey = "http.user_agent";
            public static readonly string HttpPathKey = "http.path";
            public static readonly string HttpHostKey = "http.host";
            public static readonly string HttpUrlKey = "http.url";
            public static readonly string HttpRouteKey = "http.route";
            public static readonly string HttpFlavorKey = "http.flavor";
        }

        private class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
