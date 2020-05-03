using System;
using System.Collections.Concurrent;
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

                _livenessProber = new HttpProber(_replica, probe, probe.Http);

                _livenessProberObserver = _livenessProber.ProbeResults.Subscribe(entry =>
                {
                    (var currentState, _) = ReadCurrentState();
                    switch ((entry, currentState, moveToOnSuccess))
                    {
                        case (false, _, _):
                            Kill();
                            break;
                        case (true, ReplicaState.Started, ReplicaState.Ready):
                        case (true, ReplicaState.Healthy, ReplicaState.Ready):
                            MoveToReady();
                            break;
                        case (true, ReplicaState.Started, ReplicaState.Healthy):
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

                _readinessProber = new HttpProber(_replica, probe, probe.Http);

                _readinessProberObserver = _readinessProber.ProbeResults.Subscribe(entry =>
                {
                    (var currentState, _) = ReadCurrentState();
                    switch ((entry, currentState))
                    {
                        case (false, ReplicaState.Ready):
                            MoveToHealthy(from: ReplicaState.Ready);
                            break;
                        case (true, ReplicaState.Healthy):
                            MoveToReady();
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
            private Probe _probe;
            private HttpProbe _httpProbeSettings;

            private Timer _probeTimer;
            private CancellationTokenSource _cts;

            public HttpProber(ReplicaStatus replica, Probe probe, HttpProbe httpProbeSettings)
                : base()
            {
                _replica = replica;
                _probe = probe;
                _httpProbeSettings = httpProbeSettings;

                _probeTimer = new Timer(DoProbe, null, Timeout.Infinite, Timeout.Infinite);
                _cts = new CancellationTokenSource();
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
                    //TOOD: port selection
                    foreach (var binding in _replica.Bindings)
                    {
                        var protocol = binding.Protocol ?? "http";
                        var address = $"{protocol}://localhost:{binding.Port}{_httpProbeSettings.Path}";
                        
                        var req = new HttpRequestMessage(HttpMethod.Get, address);
                        foreach (var header in _httpProbeSettings.Headers)
                        {
                            req.Headers.Add(header.Key, header.Value.ToString());
                        }

                        var res = await _httpClient.SendAsync(req);
                        if (!res.IsSuccessStatusCode)
                        {
                            ProbeResults.OnNext(false);
                            return;
                        }
                    }
                    
                    ProbeResults.OnNext(true);
                }
                catch (HttpRequestException)
                {
                    ProbeResults.OnNext(false);
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

            public override void Start()
            {
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
