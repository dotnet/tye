using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tye.Hosting.Diagnostics.Logging;
using Tye.Hosting.Diagnostics.Metrics;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Zipkin;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;
using Serilog;

namespace Tye.Hosting.Diagnostics
{
    public class DiagnosticsCollector
    {
        // This list of event sources needs to be extensible
        private static readonly string MicrosoftExtensionsLoggingProviderName = "Microsoft-Extensions-Logging";
        private static readonly string SystemRuntimeEventSourceName = "System.Runtime";
        private static readonly string MicrosoftAspNetCoreHostingEventSourceName = "Microsoft.AspNetCore.Hosting";
        private static readonly string GrpcAspNetCoreServer = "Grpc.AspNetCore.Server";
        private static readonly string DiagnosticSourceEventSource = "Microsoft-Diagnostics-DiagnosticSource";
        private static readonly string TplEventSource = "System.Threading.Tasks.TplEventSource";

        // This is the list of events for distributed tracing
        private static readonly string DiagnosticFilterString = "\"" +
          "Microsoft.AspNetCore/Microsoft.AspNetCore.Hosting.HttpRequestIn.Start@Activity1Start:-" +
            "Request.Scheme" +
            ";Request.Host" +
            ";Request.PathBase" +
            ";Request.QueryString" +
            ";Request.Path" +
            ";Request.Method" +
            ";ActivityStartTime=*Activity.StartTimeUtc.Ticks" +
            ";ActivityParentId=*Activity.ParentId" +
            ";ActivityId=*Activity.Id" +
            ";ActivitySpanId=*Activity.SpanId" +
            ";ActivityTraceId=*Activity.TraceId" +
            ";ActivityParentSpanId=*Activity.ParentSpanId" +
            ";ActivityIdFormat=*Activity.IdFormat" +
          "\r\n" +
        "Microsoft.AspNetCore/Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop@Activity1Stop:-" +
            "Response.StatusCode" +
            ";ActivityDuration=*Activity.Duration.Ticks" +
            ";ActivityId=*Activity.Id" +
        "\r\n" +
        "HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut@Event:-" +
        "\r\n" +
        "HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Start@Activity2Start:-" +
            "Request.RequestUri" +
            ";Request.Method" +
            ";Request.RequestUri.Host" +
            ";Request.RequestUri.Port" +
            ";ActivityStartTime=*Activity.StartTimeUtc.Ticks" +
            ";ActivityId=*Activity.Id" +
            ";ActivitySpanId=*Activity.SpanId" +
            ";ActivityTraceId=*Activity.TraceId" +
            ";ActivityParentSpanId=*Activity.ParentSpanId" +
            ";ActivityIdFormat=*Activity.IdFormat" +
            ";ActivityId=*Activity.Id" +
         "\r\n" +
        "HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Stop@Activity2Stop:-" +
            ";ActivityDuration=*Activity.Duration.Ticks" +
            ";ActivityId=*Activity.Id" +
        "\r\n" +

        "\"";

        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly DiagnosticOptions _options;

        public DiagnosticsCollector(Microsoft.Extensions.Logging.ILogger logger, DiagnosticOptions options)
        {
            _logger = logger;
            _options = options;
        }

        public void ProcessEvents(
            string applicationName,
            string serviceName,
            int processId,
            string replicaName,
            IDictionary<string, string> metrics,
            CancellationToken cancellationToken)
        {
            var hasEventPipe = false;

            for (var i = 0; i < 10; ++i)
            {
                if (DiagnosticsClient.GetPublishedProcesses().Contains(processId))
                {
                    hasEventPipe = true;
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Thread.Sleep(500);
            }

            if (!hasEventPipe)
            {
                _logger.LogInformation("Process id {PID}, does not support event pipe", processId);
                return;
            }

            _logger.LogInformation("Listening for event pipe events for {ServiceName} on process id {PID}", replicaName, processId);

            // Create the logger factory for this replica
            using var loggerFactory = LoggerFactory.Create(builder => ConfigureLogging(serviceName, replicaName, builder));

            var processor = new SimpleSpanProcessor(CreateSpanExporter(serviceName, replicaName));

            var providers = new List<EventPipeProvider>()
            {
                // Runtime Metrics
                new EventPipeProvider(
                    SystemRuntimeEventSourceName,
                    EventLevel.Informational,
                    (long)ClrTraceEventParser.Keywords.None,
                    new Dictionary<string, string>() {
                        { "EventCounterIntervalSec", "1" }
                    }
                ),
                new EventPipeProvider(
                    MicrosoftAspNetCoreHostingEventSourceName,
                    EventLevel.Informational,
                    (long)ClrTraceEventParser.Keywords.None,
                    new Dictionary<string, string>() {
                        { "EventCounterIntervalSec", "1" }
                    }
                ),
                new EventPipeProvider(
                    GrpcAspNetCoreServer,
                    EventLevel.Informational,
                    (long)ClrTraceEventParser.Keywords.None,
                    new Dictionary<string, string>() {
                        { "EventCounterIntervalSec", "1" }
                    }
                ),
                
                // Application Metrics
                new EventPipeProvider(
                    applicationName,
                    EventLevel.Informational,
                    (long)ClrTraceEventParser.Keywords.None,
                    new Dictionary<string, string>() {
                        { "EventCounterIntervalSec", "1" }
                    }
                ),

                // Logging
                new EventPipeProvider(
                    MicrosoftExtensionsLoggingProviderName,
                    EventLevel.LogAlways,
                    (long)(LoggingEventSource.Keywords.JsonMessage | LoggingEventSource.Keywords.FormattedMessage)
                ),

                // Distributed Tracing

                // Activity correlation
                new EventPipeProvider(TplEventSource,
                        keywords: 0x80,
                        eventLevel: EventLevel.LogAlways),

                // Diagnostic source events
                new EventPipeProvider(DiagnosticSourceEventSource,
                        keywords: 0x1 | 0x2,
                        eventLevel: EventLevel.Verbose,
                        arguments: new Dictionary<string,string>
                        {
                            { "FilterAndPayloadSpecs", DiagnosticFilterString }
                        })
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                EventPipeSession session = null;
                var client = new DiagnosticsClient(processId);

                try
                {
                    session = client.StartEventPipeSession(providers);
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                // If the process has already exited, a ServerNotAvailableException will be thrown.
                catch (ServerNotAvailableException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogDebug(0, ex, "Failed to start the event pipe session");
                    }

                    // We can't even start the session, wait until the process boots up again to start another metrics thread
                    break;
                }

                void StopSession()
                {
                    try
                    {
                        session.Stop();
                    }
                    catch (EndOfStreamException)
                    {
                        // If the app we're monitoring exits abruptly, this may throw in which case we just swallow the exception and exit gracefully.
                    }
                    // We may time out if the process ended before we sent StopTracing command. We can just exit in that case.
                    catch (TimeoutException)
                    {
                    }
                    // On Unix platforms, we may actually get a PNSE since the pipe is gone with the process, and Runtime Client Library
                    // does not know how to distinguish a situation where there is no pipe to begin with, or where the process has exited
                    // before dotnet-counters and got rid of a pipe that once existed.
                    // Since we are catching this in StopMonitor() we know that the pipe once existed (otherwise the exception would've 
                    // been thrown in StartMonitor directly)
                    catch (PlatformNotSupportedException)
                    {
                    }
                    // If the process has already exited, a ServerNotAvailableException will be thrown.
                    // This can always race with tye shutting down and a process being restarted on exiting.
                    catch (ServerNotAvailableException)
                    {
                    }
                }

                using var _ = cancellationToken.Register(() => StopSession());

                try
                {
                    var source = new EventPipeEventSource(session.EventStream);

                    // Distribued Tracing
                    HandleDistributedTracingEvents(source, processor);

                    // Metrics
                    HandleEventCounters(source, metrics);

                    // Logging
                    HandleLoggingEvents(source, loggerFactory, replicaName);

                    source.Process();
                }
                catch (DiagnosticsClientException ex)
                {
                    _logger.LogDebug(0, ex, "Failed to start the event pipe session");
                }
                catch (Exception)
                {
                    // This fails if stop is called or if the process dies
                }
                finally
                {
                    session?.Dispose();
                }
            }

            _logger.LogInformation("Event pipe collection completed for {ServiceName} on process id {PID}", replicaName, processId);
        }

        private void HandleLoggingEvents(EventPipeEventSource source, ILoggerFactory loggerFactory, string replicaName)
        {
            var lastFormattedMessage = "";

            var logActivities = new Dictionary<Guid, LogActivityItem>();
            var stack = new Stack<Guid>();

            source.Dynamic.AddCallbackForProviderEvent(MicrosoftExtensionsLoggingProviderName, "ActivityJsonStart/Start", (traceEvent) =>
            {
                var factoryId = (int)traceEvent.PayloadByName("FactoryID");
                var categoryName = (string)traceEvent.PayloadByName("LoggerName");
                var argsJson = (string)traceEvent.PayloadByName("ArgumentsJson");

                // TODO: Store this information by logger factory id
                var item = new LogActivityItem
                {
                    ActivityID = traceEvent.ActivityID,
                    ScopedObject = new LogObject(JsonDocument.Parse(argsJson).RootElement),
                };

                if (stack.TryPeek(out var parentId) && logActivities.TryGetValue(parentId, out var parentItem))
                {
                    item.Parent = parentItem;
                }

                stack.Push(traceEvent.ActivityID);
                logActivities[traceEvent.ActivityID] = item;
            });

            source.Dynamic.AddCallbackForProviderEvent(MicrosoftExtensionsLoggingProviderName, "ActivityJsonStop/Stop", (traceEvent) =>
            {
                var factoryId = (int)traceEvent.PayloadByName("FactoryID");
                var categoryName = (string)traceEvent.PayloadByName("LoggerName");

                stack.Pop();
                logActivities.Remove(traceEvent.ActivityID);
            });

            source.Dynamic.AddCallbackForProviderEvent(MicrosoftExtensionsLoggingProviderName, "MessageJson", (traceEvent) =>
            {
                // Level, FactoryID, LoggerName, EventID, EventName, ExceptionJson, ArgumentsJson
                var logLevel = (LogLevel)traceEvent.PayloadByName("Level");
                var factoryId = (int)traceEvent.PayloadByName("FactoryID");
                var categoryName = (string)traceEvent.PayloadByName("LoggerName");
                var eventId = (int)traceEvent.PayloadByName("EventId");
                var eventName = (string)traceEvent.PayloadByName("EventName");
                var exceptionJson = (string)traceEvent.PayloadByName("ExceptionJson");
                var argsJson = (string)traceEvent.PayloadByName("ArgumentsJson");

                // There's a bug that causes some of the columns to get mixed up
                if (eventName.StartsWith("{"))
                {
                    argsJson = exceptionJson;
                    exceptionJson = eventName;
                    eventName = null;
                }

                if (string.IsNullOrEmpty(argsJson))
                {
                    return;
                }

                Exception exception = null;

                var logger = loggerFactory.CreateLogger(categoryName);

                var scopes = new List<IDisposable>();

                if (logActivities.TryGetValue(traceEvent.ActivityID, out var logActivityItem))
                {
                    // REVIEW: Does order matter here? We're combining everything anyways.
                    while (logActivityItem != null)
                    {
                        scopes.Add(logger.BeginScope(logActivityItem.ScopedObject));

                        logActivityItem = logActivityItem.Parent;
                    }
                }

                try
                {
                    if (exceptionJson != "{}")
                    {
                        var exceptionMessage = JsonSerializer.Deserialize<JsonElement>(exceptionJson);
                        exception = new LoggerException(exceptionMessage);
                    }

                    var message = JsonSerializer.Deserialize<JsonElement>(argsJson);
                    if (message.TryGetProperty("{OriginalFormat}", out var formatElement))
                    {
                        var formatString = formatElement.GetString();
                        var formatter = new LogValuesFormatter(formatString);
                        var args = new object[formatter.ValueNames.Count];
                        for (var i = 0; i < args.Length; i++)
                        {
                            args[i] = message.GetProperty(formatter.ValueNames[i]).GetString();
                        }

                        logger.Log(logLevel, new EventId(eventId, eventName), exception, formatString, args);
                    }
                    else
                    {
                        var obj = new LogObject(message, lastFormattedMessage);
                        logger.Log(logLevel, new EventId(eventId, eventName), obj, exception, LogObject.Callback);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error processing log entry for {ServiceName}", replicaName);
                }
                finally
                {
                    scopes.ForEach(d => d.Dispose());
                }
            });

            source.Dynamic.AddCallbackForProviderEvent(MicrosoftExtensionsLoggingProviderName, "FormattedMessage", (traceEvent) =>
            {
                // Level, FactoryID, LoggerName, EventID, EventName, FormattedMessage
                var logLevel = (LogLevel)traceEvent.PayloadByName("Level");
                var factoryId = (int)traceEvent.PayloadByName("FactoryID");
                var categoryName = (string)traceEvent.PayloadByName("LoggerName");
                var eventId = (int)traceEvent.PayloadByName("EventId");
                var eventName = (string)traceEvent.PayloadByName("EventName");
                var formattedMessage = (string)traceEvent.PayloadByName("FormattedMessage");

                if (string.IsNullOrEmpty(formattedMessage))
                {
                    formattedMessage = eventName;
                    eventName = "";
                }

                lastFormattedMessage = formattedMessage;
            });
        }

        private void HandleEventCounters(EventPipeEventSource source, IDictionary<string, string> metrics)
        {
            source.Dynamic.All += traceEvent =>
            {
                try
                {
                    // Metrics
                    if (traceEvent.EventName.Equals("EventCounters"))
                    {
                        var payloadVal = (IDictionary<string, object>)traceEvent.PayloadValue(0);
                        var eventPayload = (IDictionary<string, object>)payloadVal["Payload"];

                        var payload = CounterPayload.FromPayload(eventPayload);

                        metrics[traceEvent.ProviderName + "/" + payload.Name] = payload.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing counter for {ProviderName}:{EventName}", traceEvent.ProviderName, traceEvent.EventName);
                }
            };
        }

        private static void HandleDistributedTracingEvents(EventPipeEventSource source, SpanProcessor processor)
        {
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
                            string method = null;
                            string path = null;
                            string host = null;
                            string pathBase = null;
                            string queryString = null;
                            string scheme = null;

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
                        string uri = null;
                        string method = null;

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
            };
        }

        private static (string ActivityId, TimeSpan Duration) GetActivityStop(IDictionary<string, object>[] arguments)
        {
            var activityId = default(string);
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

        private static bool TryCreateActivity(IDictionary<string, object>[] arguments, out ActivityItem item)
        {
            string activityId = null;
            string operationName = null;
            string spanId = null;
            string parentSpanId = null;
            string traceId = null;
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
                item = null;
                // Not a 3.1 application (we can detect this earlier)
                return false;
            }

            if (idFormat == ActivityIdFormat.Hierarchical)
            {
                // We need W3C to make it work
                item = null;
                return false;
            }

            // This is what open telemetry currently does
            // https://github.com/open-telemetry/opentelemetry-dotnet/blob/4ba732af062ddc2759c02aebbc91335aaa3f7173/src/OpenTelemetry.Collector.AspNetCore/Implementation/HttpInListener.cs#L65-L92

            item = new ActivityItem()
            {
                Id = activityId,
                Name = operationName,
                SpanId = ActivitySpanId.CreateFromString(spanId),
                TraceId = ActivityTraceId.CreateFromString(traceId),
                ParentSpanId = parentSpanId == "0000000000000000" ? default : ActivitySpanId.CreateFromString(parentSpanId),
                StartTime = startTime,
            };

            return true;
        }

        // This is the logger factory for application logs. It allows re-routing event pipe collected logs (structured logs)
        // to any of the supported sinks, currently (elastic search and app insights)
        private void ConfigureLogging(string serviceName, string replicaName, ILoggingBuilder builder)
        {
            var logProviderKey = _options.LoggingProvider.Key;
            var logProviderValue = _options.LoggingProvider.Value;

            if (string.Equals(logProviderKey, "elastic", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(logProviderValue))
            {
                var loggerConfiguration = new LoggerConfiguration()
                                            .Enrich.WithProperty("Application", serviceName)
                                            .Enrich.WithProperty("Instance", replicaName)
                                            .Enrich.FromLogContext()
                                            .WriteTo.Elasticsearch(logProviderValue);

                builder.AddSerilog(loggerConfiguration.CreateLogger());
            }

            if (string.Equals(logProviderKey, "console", StringComparison.OrdinalIgnoreCase))
            {
                var loggerConfiguration = new LoggerConfiguration()
                                            .Enrich.WithProperty("Application", serviceName)
                                            .Enrich.WithProperty("Instance", replicaName)
                                            .Enrich.FromLogContext()
                                            .WriteTo.Console(outputTemplate: "[{Instance}]: [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

                builder.AddSerilog(loggerConfiguration.CreateLogger());
            }

            if (string.Equals(logProviderKey, "seq", StringComparison.OrdinalIgnoreCase))
            {
                var loggerConfiguration = new LoggerConfiguration()
                                            .Enrich.WithProperty("Application", serviceName)
                                            .Enrich.WithProperty("Instance", replicaName)
                                            .Enrich.FromLogContext()
                                            .WriteTo.Seq(logProviderValue);

                builder.AddSerilog(loggerConfiguration.CreateLogger());
            }

            if (string.Equals(logProviderKey, "ai", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(logProviderValue))
            {
                builder.AddApplicationInsights(logProviderValue);
            }

            // REVIEW: How are log levels controlled on the outside?
            builder.SetMinimumLevel(LogLevel.Information);
        }


        private SpanExporter CreateSpanExporter(string serviceName, string replicaName)
        {
            if (string.Equals(_options.DistributedTraceProvider.Key, "zipkin", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(_options.DistributedTraceProvider.Value))
            {
                var zipkin = new ZipkinTraceExporter(new ZipkinTraceExporterOptions
                {
                    ServiceName = serviceName,
                    Endpoint = new Uri($"{_options.DistributedTraceProvider.Value.TrimEnd('/')}/api/v2/spans")
                });

                return zipkin;
            }

            // TODO: Support Jaegar
            // TODO: Support ApplicationInsights

            return new NullExporter();
        }

        public class NullExporter : SpanExporter
        {
            public override Task<ExportResult> ExportAsync(IEnumerable<SpanData> batch, CancellationToken cancellationToken)
            {
                return Task.FromResult(ExportResult.Success);
            }

            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private class ActivityItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public ActivityTraceId TraceId { get; set; }
            public ActivitySpanId SpanId { get; set; }
            public Dictionary<string, object> Attributes { get; } = new Dictionary<string, object>();
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public SpanKind Kind { get; set; }

            public ActivitySpanId ParentSpanId { get; set; }
        }

        private class LogActivityItem
        {
            public Guid ActivityID { get; set; }

            public LogObject ScopedObject { get; set; }

            public LogActivityItem Parent { get; set; }
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

        internal sealed class LoggingEventSource
        {
            /// <summary>
            /// This is public from an EventSource consumer point of view, but since these defintions
            /// are not needed outside this class
            /// </summary>
            public static class Keywords
            {
                /// <summary>
                /// Meta events are events about the LoggingEventSource itself (that is they did not come from ILogger
                /// </summary>
                public const EventKeywords Meta = (EventKeywords)1;
                /// <summary>
                /// Turns on the 'Message' event when ILogger.Log() is called.   It gives the information in a programmatic (not formatted) way
                /// </summary>
                public const EventKeywords Message = (EventKeywords)2;
                /// <summary>
                /// Turns on the 'FormatMessage' event when ILogger.Log() is called.  It gives the formatted string version of the information.
                /// </summary>
                public const EventKeywords FormattedMessage = (EventKeywords)4;
                /// <summary>
                /// Turns on the 'MessageJson' event when ILogger.Log() is called.   It gives  JSON representation of the Arguments.
                /// </summary>
                public const EventKeywords JsonMessage = (EventKeywords)8;
            }
        }
    }
}
