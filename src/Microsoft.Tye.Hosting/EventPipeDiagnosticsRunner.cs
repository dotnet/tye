// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Diagnostics;
using Microsoft.Tye.Hosting.Model;
using Microsoft.Extensions.Logging;

namespace Microsoft.Tye.Hosting
{
    public class EventPipeDiagnosticsRunner : IApplicationProcessor
    {
        private readonly ILogger _logger;
        private readonly DiagnosticsCollector _diagnosticsCollector;

        public EventPipeDiagnosticsRunner(ILogger logger, DiagnosticsCollector diagnosticsCollector)
        {
            _logger = logger;
            _diagnosticsCollector = diagnosticsCollector;
        }

        public Task StartAsync(Application application)
        {
            foreach (var service in application.Services.Values)
            {
                if (service.Description.RunInfo is null)
                {
                    continue;
                }

                service.Items[typeof(Subscription)] = service.ReplicaEvents.Subscribe(OnReplicaChanged);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(Application application)
        {
            foreach (var service in application.Services.Values)
            {
                if (service.Items.TryGetValue(typeof(Subscription), out var item) && item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            return Task.CompletedTask;
        }

        private void OnReplicaChanged(ReplicaEvent replicaEvent)
        {
            var replica = replicaEvent.Replica;

            if (!(replica is ProcessStatus process))
            {
                // This temporarily only works for processes launched
                return;
            }

            switch (replicaEvent.State)
            {
                case ReplicaState.Started:
                    {
                        var cts = new CancellationTokenSource();
                        var state = new DiagnosticsState
                        {
                            StoppingTokenSource = cts,
                            Thread = new Thread(() =>
                            {
                                // TODO: Finding the application name requires msbuild knowledge
                                _diagnosticsCollector.ProcessEvents(
                                    Path.GetFileNameWithoutExtension(process.Service.Status.ProjectFilePath),
                                    process.Service.Description.Name,
                                    process.Pid!.Value,
                                    replica.Name,
                                    replica.Metrics,
                                    cts.Token);
                            })
                        };

                        replica.Items[typeof(DiagnosticsState)] = state;

                        state.Thread.Start();
                    }

                    break;
                case ReplicaState.Stopped:
                    {
                        if (replica.Items.TryGetValue(typeof(DiagnosticsState), out var item) && item is DiagnosticsState state)
                        {
                            state.StoppingTokenSource.Cancel();

                            state.Thread.Join();
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        // Used a lookup key for state
        private class Subscription { }
        private class DiagnosticsState
        {
            public Thread Thread { get; set; } = default!;
            public CancellationTokenSource StoppingTokenSource { get; set; } = new CancellationTokenSource();
        }
    }
}
