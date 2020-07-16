// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using static Microsoft.Tye.Hosting.Diagnostics.WellKnownEventSources;

namespace Microsoft.Tye.Hosting.Diagnostics
{
    public class DiagnosticsCollector
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public DiagnosticsCollector(Microsoft.Extensions.Logging.ILogger logger)
        {
            _logger = logger;
        }

        public LoggingSink? LoggingSink { get; set; }

        public MetricSink? MetricSink { get; set; }

        public TracingSink? TracingSink { get; set; }

        public TimeSpan SelectProcessTimeout { get; set; } = Debugger.IsAttached ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(5);

        public Task CollectAsync(ReplicaInfo replicaInfo, CancellationToken cancellationToken)
        {
            // The diagnostic collection process does lots of synchronous processing. Explicitly
            // create a thread so we don't starve thread pool.
            var tcs = new TaskCompletionSource<object?>();
            var thread = new Thread(() =>
            {
                try
                {
                    Collect(replicaInfo, cancellationToken);
                    tcs.SetResult(null);
                }
                catch (OperationCanceledException)
                {
                    tcs.SetCanceled();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            thread.Start();
            return tcs.Task;
        }

        public void Collect(ReplicaInfo replicaInfo, CancellationToken cancellationToken)
        {
            var start = DateTime.UtcNow;

            var processId = (int?)null;
            while (DateTime.UtcNow < start + SelectProcessTimeout && !cancellationToken.IsCancellationRequested)
            {
                processId = SelectProcess(replicaInfo);
                if (processId.HasValue)
                {
                    _logger.LogInformation("Selected process {PID}.", processId);
                    break;
                }

                _logger.LogInformation("No process was selected. Waiting.");
                Thread.Sleep(500);
            }

            if (processId is null)
            {
                _logger.LogInformation("Failed to select a process after {Timeout}.", SelectProcessTimeout);
                return;
            }

            // When we get here we've chosen the desired process.
            _logger.LogInformation("Listening for event pipe events for {ServiceName} on process id {PID}", replicaInfo.Replica, processId);

            var providers = CreateDefaultProviders();
            providers.Add(CreateStandardProvider(replicaInfo.AssemblyName));

            while (!cancellationToken.IsCancellationRequested)
            {
                var session = (EventPipeSession?)null;
                var client = new DiagnosticsClient(processId.Value);

                try
                {
                    session = client.StartEventPipeSession(providers);
                }
                // If the process has already exited, a ServerNotAvailableException will be thrown.
                catch (Exception)
                {
                    break;
                }

                void StopSession()
                {
                    try
                    {
                        session?.Stop();
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

                var disposables = new List<IDisposable>();
                try
                {
                    var source = new EventPipeEventSource(session.EventStream);

                    // Distributed Tracing
                    if (TracingSink is object)
                    {
                        disposables.Add(TracingSink.Attach(source, replicaInfo));
                    }

                    // Metrics
                    if (MetricSink is object)
                    {
                        disposables.Add(MetricSink.Attach(source, replicaInfo));
                    }

                    // Logging
                    if (LoggingSink is object)
                    {
                        disposables.Add(LoggingSink.Attach(source, replicaInfo));
                    }

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

                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }

            _logger.LogInformation("Event pipe collection completed for {ServiceName} on process id {PID}", replicaInfo.Replica, processId);
        }

        private static int? SelectProcess(ReplicaInfo replicaInfo)
        {
            var processIds = DiagnosticsClient.GetPublishedProcesses();
            var processes = processIds.Select(pid =>
            {
                try
                {
                    return Process.GetProcessById(pid);
                }
                catch (Exception) // Can fail due to timing.
                {
                    return null;
                }
            })
            .Where(p => p is object)
            .ToArray();

            try
            {
                return replicaInfo.Selector.Invoke(processes!)?.Id;
            }
            finally
            {
                foreach (var process in processes!)
                {
                    process!.Dispose();
                }
            }
        }
    }
}
