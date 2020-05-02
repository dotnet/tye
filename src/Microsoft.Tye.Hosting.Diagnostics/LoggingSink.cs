// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Diagnostics.Logging;
using Serilog;
using static Microsoft.Tye.Hosting.Diagnostics.WellKnownEventSources;

namespace Microsoft.Tye.Hosting.Diagnostics
{
    public class LoggingSink
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly DiagnosticsProvider _provider;

        public LoggingSink(Microsoft.Extensions.Logging.ILogger logger, DiagnosticsProvider provider)
        {
            _logger = logger;
            _provider = provider;
        }

        public IDisposable Attach(EventPipeEventSource source, ReplicaInfo replicaInfo)
        {
            using var loggerFactory = LoggerFactory.Create(builder => ConfigureLogging(replicaInfo.Service, replicaInfo.Replica, builder));

            var lastFormattedMessage = "";

            var logActivities = new Dictionary<Guid, LogActivityItem>();
            var stack = new Stack<Guid>();

            source.Dynamic.AddCallbackForProviderEvent(MicrosoftExtensionsLoggingProviderName, "ActivityJsonStart/Start", (traceEvent) =>
            {
                var factoryId = (int)traceEvent.PayloadByName("FactoryID");
                var categoryName = (string)traceEvent.PayloadByName("LoggerName");
                var argsJson = (string)traceEvent.PayloadByName("ArgumentsJson");

                // TODO: Store this information by logger factory id
                var item = new LogActivityItem(traceEvent.ActivityID, new LogObject(JsonDocument.Parse(argsJson).RootElement));
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
                    eventName = null!;
                }

                if (string.IsNullOrEmpty(argsJson))
                {
                    return;
                }

                Exception? exception = null;

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
                            if (!message.TryGetProperty(formatter.ValueNames[i], out var argValue))
                            {
                                // We couldn't find the parsed property in the original message, it's likely that this is a JSON object or something else
                                // being logged, or some other format that just looks like the formatted message. Stop here and log the formatted message
                                var obj = new LogObject(message, lastFormattedMessage);
                                logger.Log(logLevel, new EventId(eventId, eventName), obj, exception, LogObject.Callback);

                                break;
                            }

                            args[i] = argValue.GetString();
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
                    _logger.LogDebug(ex, "Error processing log entry for {ServiceName}", replicaInfo.Replica);
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

            return loggerFactory; // the logger factory will be cleaned up when collection ends.
        }

        // This is the logger factory for application logs. It allows re-routing event pipe collected logs (structured logs)
        // to any of the supported sinks, currently (elastic search and app insights)
        private void ConfigureLogging(string serviceName, string replicaName, ILoggingBuilder builder)
        {
            if (string.Equals(_provider.Key, "elastic", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(_provider.Value))
            {
                var loggerConfiguration = new LoggerConfiguration()
                    .Enrich.WithProperty("Application", serviceName)
                    .Enrich.WithProperty("Instance", replicaName)
                    .Enrich.FromLogContext()
                    .WriteTo.Elasticsearch(_provider.Value);

                builder.AddSerilog(loggerConfiguration.CreateLogger());
            }

            if (string.Equals(_provider.Key, "console", StringComparison.OrdinalIgnoreCase))
            {
                var loggerConfiguration = new LoggerConfiguration()
                    .Enrich.WithProperty("Application", serviceName)
                    .Enrich.WithProperty("Instance", replicaName)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(outputTemplate: "[{Instance}]: [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

                builder.AddSerilog(loggerConfiguration.CreateLogger());
            }

            if (string.Equals(_provider.Key, "seq", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(_provider.Value))
            {
                var loggerConfiguration = new LoggerConfiguration()
                    .Enrich.WithProperty("Application", serviceName)
                    .Enrich.WithProperty("Instance", replicaName)
                    .Enrich.FromLogContext()
                    .WriteTo.Seq(_provider.Value);

                builder.AddSerilog(loggerConfiguration.CreateLogger());
            }

            if (string.Equals(_provider.Key, "ai", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(_provider.Value))
            {
                builder.AddApplicationInsights(_provider.Value);
            }

            // REVIEW: How are log levels controlled on the outside?
            builder.SetMinimumLevel(LogLevel.Information);
        }

        private class LogActivityItem
        {
            public LogActivityItem(Guid activityID, LogObject scopedObject)
            {
                ActivityID = activityID;
                ScopedObject = scopedObject;
            }

            public Guid ActivityID { get; }

            public LogObject ScopedObject { get; }

            public LogActivityItem? Parent { get; set; }
        }
    }
}
