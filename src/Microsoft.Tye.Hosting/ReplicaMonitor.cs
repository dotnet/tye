// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class ReplicaMonitor : IApplicationProcessor
    {
        private ILogger _logger;
        private ConcurrentDictionary<string, ReplicaMonitorState> _states;

        public ReplicaMonitor(ILogger logger)
        {
            _logger = logger;
            _states = new ConcurrentDictionary<string, ReplicaMonitorState>();
        }

        public Task StartAsync(Application application)
        {
            foreach (var service in application.Services.Values)
            {
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
            switch (replicaEvent.State)
            {
                case ReplicaState.Started:
                    _states.TryAdd(replicaEvent.Replica.Name, new ReplicaMonitorState(replicaEvent.Replica, _logger));
                    break;
                case ReplicaState.Stopped:
                    if (_states.TryRemove(replicaEvent.Replica.Name, out var stateToDispose))
                    {
                        stateToDispose.Dispose();
                    }
                    break;
                default:
                    if (_states.TryGetValue(replicaEvent.Replica.Name, out var state))
                    {
                        state.Update(replicaEvent);
                    }
                    break;
            }
        }

        private class Subscription
        {
        }

        private class ReplicaMonitorState : IDisposable
        {
            private ReplicaStatus _replica;
            private ILogger _logger;

            private Prober? _livenessProber;
            private Prober? _readinessProber;
            private IDisposable? _livenessProberObserver;
            private IDisposable? _readinessProberObserver;

            private ReplicaState _currentState;
            private DateTime _lastStateChange;
            private object _stateChangeLocker;

            public ReplicaMonitorState(ReplicaStatus replica, ILogger logger)
            {
                _replica = replica;
                _logger = logger;

                _currentState = ReplicaState.Started;
                _lastStateChange = DateTime.Now;
                _stateChangeLocker = new object();

                Init();
            }

            private void Init()
            {
                var serviceDesc = _replica.Service.Description;
                if (serviceDesc.Liveness is null && serviceDesc.Readiness is null)
                {
                    MoveToReady();
                }
                else if (serviceDesc.Liveness is null)
                {
                    MoveToHealthy(from: ReplicaState.Started);
                    StartReadinessProbe(serviceDesc.Readiness!);
                }
                else if (serviceDesc.Readiness is null)
                {
                    StartLivenessProbe(serviceDesc.Liveness, moveToOnSuccess: ReplicaState.Ready);
                }
                else
                {
                    StartLivenessProbe(serviceDesc.Liveness);
                    StartReadinessProbe(serviceDesc.Readiness);
                }
            }

            private void StartLivenessProbe(Probe probe, ReplicaState moveToOnSuccess = ReplicaState.Healthy)
            {
                // currently only HTTP is available
                if (probe.Http is null)
                {
                    _logger.LogWarning("cannot start probing replica {name} because probe configuration is not set", _replica.Name);
                    return;
                }

                _livenessProber = new HttpProber(_replica, "liveness", probe, probe.Http, _logger);

                var failureThreshold = probe.FailureThreshold;
                var failures = 0;
                var dead = false;

                _livenessProberObserver = _livenessProber.ProbeResults.Subscribe(entry =>
                {
                    if (dead)
                    {
                        return;
                    }

                    if (entry)
                    {
                        // Reset failures count on success
                        failures = 0;
                    }

                    (var currentState, _) = ReadCurrentState();
                    var failuresPastThreshold = failures >= failureThreshold;
                    switch ((entry, currentState, moveToOnSuccess, failuresPastThreshold))
                    {
                        case (false, _, _, true):
                            dead = true;
                            Kill();
                            break;
                        case (false, _, _, false):
                            Interlocked.Increment(ref failures);
                            break;
                        case (true, ReplicaState.Started, ReplicaState.Ready, _):
                        case (true, ReplicaState.Healthy, ReplicaState.Ready, _):
                            MoveToReady();
                            break;
                        case (true, ReplicaState.Started, ReplicaState.Healthy, _):
                            MoveToHealthy(from: ReplicaState.Started);
                            break;
                    }
                });

                _livenessProber.Start();
            }

            private void StartReadinessProbe(Probe probe)
            {
                // currently only HTTP is available
                if (probe.Http is null)
                {
                    _logger.LogWarning("cannot start probing replica {name} because probe configuration is not set", _replica.Name);
                    return;
                }

                _readinessProber = new HttpProber(_replica, "readiness", probe, probe.Http, _logger);

                var successThreshold = probe.SuccessThreshold;
                var failureThreshold = probe.FailureThreshold;

                var successes = 0;
                var failures = 0;

                _readinessProberObserver = _readinessProber.ProbeResults.Subscribe(entry =>
                {
                    if (entry)
                    {
                        // Reset failures count on success
                        failures = 0;
                    }
                    else
                    {
                        // Reset successes count on failure
                        successes = 0;
                    }

                    (var currentState, _) = ReadCurrentState();
                    var successesPastThreshold = successes >= successThreshold;
                    var failuresPastThreshold = failures >= failureThreshold;
                    switch ((entry, currentState, failuresPastThreshold, successesPastThreshold))
                    {
                        case (false, ReplicaState.Ready, true, _):
                            MoveToHealthy(from: ReplicaState.Ready);
                            break;
                        case (false, ReplicaState.Ready, false, _):
                            Interlocked.Increment(ref failures);
                            break;
                        case (true, ReplicaState.Healthy, _, true):
                            MoveToReady();
                            break;
                        case (true, ReplicaState.Healthy, _, false):
                            Interlocked.Increment(ref successes);
                            break;
                    }
                });

                _readinessProber.Start();
            }

            private void MoveToHealthy(ReplicaState from)
            {
                _logger.LogInformation("replica {name} is moving to an healthy state", _replica.Name);
                ChangeState(ReplicaState.Healthy);
            }

            private void MoveToReady()
            {
                _logger.LogInformation("replica {name} is moving to a ready state", _replica.Name);
                ChangeState(ReplicaState.Ready);
            }

            private void Kill()
            {
                _logger.LogInformation("killing replica {name} because it has failed the liveness probe", _replica.Name);
                _replica.StoppingTokenSource.Cancel();
            }

            private void ChangeState(ReplicaState state)
            {
                _replica.Service.ReplicaEvents.OnNext(new ReplicaEvent(state, _replica));
                lock (_stateChangeLocker)
                {
                    _currentState = state;
                    _lastStateChange = DateTime.Now;
                }
            }

            private (ReplicaState state, DateTime lastChanged) ReadCurrentState()
            {
                lock (_stateChangeLocker)
                {
                    return (_currentState, _lastStateChange);
                }
            }

            public void Update(ReplicaEvent replicaEvent)
            {
            }

            public void Dispose()
            {
                _livenessProber?.Dispose();
                _readinessProber?.Dispose();
                _livenessProberObserver?.Dispose();
                _readinessProberObserver?.Dispose();
            }
        }

        private abstract class Prober : IDisposable
        {
            protected Prober()
            {
                ProbeResults = new Subject<bool>();
            }

            public Subject<bool> ProbeResults { get; }

            public abstract void Start();

            public abstract void Dispose();
        }

        private class HttpProber : Prober
        {
            private static HttpClient _httpClient;

            static HttpProber()
            {
                _httpClient = new HttpClient();
            }

            private ReplicaStatus _replica;
            private ReplicaBinding? _selectedBinding;
            private string _probeName;
            private Probe _probe;
            private Model.HttpProber _httpProberSettings;

            private Timer _probeTimer;
            private CancellationTokenSource _cts;

            private ILogger _logger;

            private bool _lastStatus;

            public HttpProber(ReplicaStatus replica, string probeName, Probe probe, Model.HttpProber httpProberSettings, ILogger logger)
                : base()
            {
                _replica = replica;
                _selectedBinding = null;
                _probeName = probeName;
                _probe = probe;
                _httpProberSettings = httpProberSettings;

                _probeTimer = new Timer(DoProbe, null, Timeout.Infinite, Timeout.Infinite);
                _cts = new CancellationTokenSource();

                _logger = logger;

                _lastStatus = true;
            }

            private void DoProbe(object? state)
            {
                _probeTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _ = DoProbeAsync();
            }

            private async Task DoProbeAsync()
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    var protocol = _selectedBinding!.Protocol;
                    var address = $"{protocol}://localhost:{_selectedBinding.Port}{_httpProberSettings.Path}";

                    using var timeoutCts = new CancellationTokenSource(_probe.Timeout);
                    var req = new HttpRequestMessage(HttpMethod.Get, address);
                    foreach (var header in _httpProberSettings.Headers)
                    {
                        req.Headers.Add(header.Key, header.Value.ToString());
                    }

                    var res = await _httpClient.SendAsync(req, timeoutCts.Token);
                    if (!res.IsSuccessStatusCode)
                    {
                        ShowWarning($"replica {_replica.Name} failed http probe at address '{_httpProberSettings.Path}' due to a failed status ({res.StatusCode})");
                        Send(false);
                        return;
                    }

                    Send(true);
                }
                catch (HttpRequestException ex)
                {
                    ShowWarning($"replica {_replica.Name} failed http probe at address '{_httpProberSettings.Path}' due to an http exception", ex);
                    Send(false);
                }
                catch (TaskCanceledException)
                {
                    ShowWarning($"replica {_replica.Name} failed http probe at address '{_httpProberSettings.Path}' due to timeout");
                    Send(false);
                }
                finally
                {
                    try
                    {
                        _probeTimer.Change(_probe.Period, Timeout.InfiniteTimeSpan);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }

            private void Send(bool status)
            {
                ProbeResults.OnNext(status);
                _lastStatus = status;
            }

            private void ShowWarning(string message, Exception? ex = null)
            {
                if (!_lastStatus)
                {
                    return;
                }

                if (ex != null)
                {
                    _logger.LogWarning(ex, message);
                }
                else
                {
                    _logger.LogWarning(message);
                }
            }

            public override void Start()
            {
                Func<ReplicaBinding, bool> bindingClosure = (_httpProberSettings.Port.HasValue, _httpProberSettings.Protocol != null) switch
                {
                    (false, false) => _ => true,
                    (true, false) => r => r.ExternalPort == _httpProberSettings.Port!.Value,
                    (false, true) => r => r.Protocol == _httpProberSettings.Protocol!,
                    (true, true) => r => r.ExternalPort == _httpProberSettings.Port!.Value && r.Protocol == _httpProberSettings.Protocol!
                };

                var selectedBindings = _replica.Bindings.Where(bindingClosure);
                if (selectedBindings.Count() == 0)
                {
                    _logger.LogWarning($"no suitable binding was found for replica {_replica.Name} for probe '{_probeName}'");
                    return;
                }

                _selectedBinding = selectedBindings.First();

                try
                {
                    _probeTimer.Change(_probe.InitialDelay, Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException)
                {
                }
            }

            public override void Dispose()
            {
                _cts.Cancel();
                _probeTimer.Dispose();
            }
        }
    }
}
