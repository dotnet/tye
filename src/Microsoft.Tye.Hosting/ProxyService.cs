// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class ProxyService : IApplicationProcessor
    {
        private IHost? _host;
        private readonly ILogger _logger;

        private ConcurrentDictionary<int, CancellationTokenSource> _cancellationsByReplicaPort;

        public ProxyService(ILogger logger)
        {
            _logger = logger;
            _cancellationsByReplicaPort = new ConcurrentDictionary<int, CancellationTokenSource>();
        }

        public Task StartAsync(Application application)
        {
            _host = new HostBuilder()
                    .ConfigureServer(server =>
                    {
                        server.UseSockets(sockets =>
                        {
                            foreach (var service in application.Services.Values)
                            {
                                if (service.Description.RunInfo == null)
                                {
                                    continue;
                                }

                                service.Items[typeof(Subscription)] = service.ReplicaEvents.Subscribe(OnReplicaEvent);

                                foreach (var binding in service.Description.Bindings)
                                {
                                    if (binding.Port == null)
                                    {
                                        // There's no port so nothing to proxy
                                        continue;
                                    }

                                    if (service.Description.Readiness == null && service.Description.Replicas == 1)
                                    {
                                        // No need to proxy for a single replica, we may want to do this later but right now we skip it
                                        continue;
                                    }

                                    if (string.Equals(binding.Protocol, "udp", StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        throw new CommandException("Proxy does not support the udp protocol yet.");
                                    }

                                    var ports = binding.ReplicaPorts;

                                    // We need to bind to all interfaces on linux since the container -> host communication won't work
                                    // if we use the IP address to reach out of the host. This works fine on osx and windows
                                    // but doesn't work on linux.
                                    var host = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? IPAddress.Any : IPAddress.Loopback;

                                    sockets.Listen(host, binding.Port.Value, o =>
                                    {
                                        long count = 0;

                                        // o.UseConnectionLogging("Tye.Proxy");

                                        o.Run(async connection =>
                                        {
                                            var notificationFeature = connection.Features.Get<IConnectionLifetimeNotificationFeature>();

                                            var next = (int)(Interlocked.Increment(ref count) % ports.Count);

                                            if (!_cancellationsByReplicaPort.TryGetValue(ports[next], out var cts))
                                            {
                                                // replica in ready state <=> it's ports have cancellation tokens in the dictionary
                                                // if replica is not in ready state, we don't forward traffic, but return instead
                                                return;
                                            }

                                            using var _ = cts.Token.Register(() => notificationFeature?.RequestClose());

                                            NetworkStream? targetStream = null;

                                            try
                                            {
                                                var target = new Socket(SocketType.Stream, ProtocolType.Tcp)
                                                {
                                                    NoDelay = true
                                                };
                                                var port = ports[next];

                                                _logger.LogDebug("Attempting to connect to {ServiceName} listening on {ExternalPort}:{Port}", service.Description.Name, binding.Port, port);

                                                await target.ConnectAsync(IPAddress.Loopback, port);

                                                _logger.LogDebug("Successfully connected to {ServiceName} listening on {ExternalPort}:{Port}", service.Description.Name, binding.Port, port);

                                                targetStream = new NetworkStream(target, ownsSocket: true);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogDebug(ex, "Proxy error for service {ServiceName}", service.Description.Name);

                                                if (targetStream is object)
                                                {
                                                    await targetStream.DisposeAsync();
                                                }

                                                connection.Abort();
                                                return;
                                            }

                                            try
                                            {
                                                _logger.LogDebug("Proxying traffic to {ServiceName} {ExternalPort}:{InternalPort}", service.Description.Name, binding.Port, ports[next]);

                                                // external -> internal
                                                var reading = Task.Run(() => connection.Transport.Input.CopyToAsync(targetStream, notificationFeature?.ConnectionClosedRequested ?? default));

                                                // internal -> external
                                                var writing = Task.Run(() => targetStream.CopyToAsync(connection.Transport.Output, notificationFeature?.ConnectionClosedRequested ?? default));

                                                await Task.WhenAll(reading, writing);
                                            }
                                            catch (ConnectionResetException)
                                            {
                                                // Connection was reset
                                            }
                                            catch (IOException)
                                            {
                                                // Reset can also appear as an IOException with an inner SocketException
                                            }
                                            catch (OperationCanceledException ex)
                                            {
                                                if (notificationFeature is null || !notificationFeature.ConnectionClosedRequested.IsCancellationRequested)
                                                {
                                                    _logger.LogDebug(0, ex, "Proxy error for service {ServiceName}", service.Description.Name);
                                                }

                                                _logger.LogDebug("Existing proxy {ServiceName} {ExternalPort}:{InternalPort}", service.Description.Name, binding.Port, ports[next]);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogDebug(0, ex, "Proxy error for service {ServiceName}", service.Description.Name);
                                            }
                                            finally
                                            {
                                                await targetStream.DisposeAsync();
                                            }

                                            // This needs to reconnect to the target port(s) until its bound
                                            // it has to stop if the service is no longer running
                                        });
                                    });
                                }
                            }
                        });
                    })
                    .Build();

            return _host.StartAsync();
        }

        public async Task StopAsync(Application application)
        {
            foreach (var service in application.Services.Values)
            {
                if (service.Items.TryGetValue(typeof(Subscription), out var item) && item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            if (_host != null)
            {
                try
                {
                    await _host.StopAsync();
                }
                catch (AggregateException)
                {
                }
                catch (ObjectDisposedException)
                {
                    // System.ObjectDisposedException: Cannot access a disposed object.
                    // From Bedrock.Framework. As we plan on removing this long term, not going to directly fix in bedrock.
                }

                if (_host is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync();
                }
            }
        }

        private void OnReplicaEvent(ReplicaEvent replicaEvent)
        {
            // when a replica becomes ready for the first time, it shouldn't have a cancellation token in the dictionary
            // for any event other than ready, we want to cancel the token and remove it from the dictionary
            foreach (var binding in replicaEvent.Replica.Bindings)
            {
                if (_cancellationsByReplicaPort.TryRemove(binding.Port, out var cts))
                {
                    cts.Cancel();
                }
            }

            if (replicaEvent.State == ReplicaState.Ready)
            {
                foreach (var binding in replicaEvent.Replica.Bindings)
                {
                    _cancellationsByReplicaPort.TryAdd(binding.Port, new CancellationTokenSource());
                }
            }
        }

        private class Subscription
        {
        }
    }
}
