// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class ProxyService : IApplicationProcessor
    {
        private IHost? _host;
        private readonly ILogger _logger;

        public ProxyService(ILogger logger)
        {
            _logger = logger;
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

                                foreach (var binding in service.Description.Bindings)
                                {
                                    if (binding.Port == null)
                                    {
                                        // There's no port so nothing to proxy
                                        continue;
                                    }

                                    if (service.Description.Replicas == 1)
                                    {
                                        // No need to proxy for a single replica, we may want to do this later but right now we skip it
                                        continue;
                                    }

                                    var ports = binding.ReplicaPorts;

                                    sockets.Listen(IPAddress.Loopback, binding.Port.Value, o =>
                                    {
                                        long count = 0;

                                        // o.UseConnectionLogging("Tye.Proxy");

                                        o.Run(async connection =>
                                        {
                                            var notificationFeature = connection.Features.Get<IConnectionLifetimeNotificationFeature>();

                                            var next = (int)(Interlocked.Increment(ref count) % ports.Count);

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
                                                var reading = Task.Run(() => connection.Transport.Input.CopyToAsync(targetStream, notificationFeature.ConnectionClosedRequested));

                                                // internal -> external
                                                var writing = Task.Run(() => targetStream.CopyToAsync(connection.Transport.Output, notificationFeature.ConnectionClosedRequested));

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
                                                if (!notificationFeature.ConnectionClosedRequested.IsCancellationRequested)
                                                {
                                                    _logger.LogDebug(0, ex, "Proxy error for service {ServiceName}", service.Description.Name);
                                                }
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
            if (_host != null)
            {
                await _host.StopAsync();

                if (_host is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync();
                }
            }
        }
    }
}
