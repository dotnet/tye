// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Proxy;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public partial class HttpProxyService : IApplicationProcessor
    {
        private List<IHost> _webApplications = new List<IHost>();
        private readonly ILogger _logger;

        private ConcurrentDictionary<int, bool> _readyPorts;

        public HttpProxyService(ILogger logger)
        {
            _logger = logger;
            _readyPorts = new ConcurrentDictionary<int, bool>();
        }

        public async Task StartAsync(Application application)
        {
            var invoker = new HttpMessageInvoker(new ConnectionRetryHandler(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseProxy = false,
                UseCookies = false,
            }));

            foreach (var service in application.Services.Values)
            {
                var serviceDescription = service.Description;

                if (service.Description.RunInfo is IngressRunInfo runInfo)
                {
                    var host = Host.CreateDefaultBuilder()
                        .ConfigureWebHostDefaults(builder =>
                        {
                            var urls = new List<string>();

                            // Bind to the addresses on this resource
                            for (int i = 0; i < serviceDescription.Replicas; i++)
                            {
                                // Fake replicas since it's all running processes
                                var replica = service.Description.Name + "_" + Guid.NewGuid().ToString().Substring(0, 10).ToLower();
                                var status = new IngressStatus(service, replica);
                                service.Replicas[replica] = status;

                                var ports = new List<int>();

                                foreach (var binding in serviceDescription.Bindings)
                                {
                                    if (binding.Port == null)
                                    {
                                        continue;
                                    }

                                    var port = binding.ReplicaPorts[i];
                                    ports.Add(port);

                                    var url = $"{binding.Protocol}://{binding.IPAddress ?? "localhost"}:{port}";
                                    urls.Add(url);
                                }

                                status.Ports = ports;

                                service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Added, status));
                            }

                            builder.ConfigureServices(services =>
                            {
                                services.AddSingleton<MatcherPolicy, IngressHostMatcherPolicy>();
                                services.AddLogging(loggingBuilder =>
                                {
                                    loggingBuilder.AddProvider(new ServiceLoggerProvider(service.Logs));
                                });

                                services.Configure<IServerAddressesFeature>(serverAddresses =>
                                {
                                    var addresses = serverAddresses.Addresses;
                                    if (addresses.IsReadOnly)
                                    {
                                        throw new NotSupportedException("Changing the URL isn't supported.");
                                    }
                                    addresses.Clear();
                                    foreach (var u in urls)
                                    {
                                        addresses.Add(u);
                                    }
                                });
                            });

                            builder.UseUrls(urls.ToArray());

                            builder.Configure(app =>
                            {
                                app.UseWebSockets();

                                app.UseRouting();

                                app.UseEndpoints(endpointBuilder =>
                                {
                                    foreach (var rule in runInfo.Rules)
                                    {
                                        if (!application.Services.TryGetValue(rule.Service, out var target))
                                        {
                                            continue;
                                        }

                                        _logger.LogInformation("Processing ingress rule: Path:{Path}, Host:{Host}, Service:{Service}", rule.Path, rule.Host, rule.Service);

                                        var targetServiceDescription = target.Description;
                                        RegisterListener(target);

                                        var uris = new List<(int Port, Uri Uri)>();

                                        // HTTP before HTTPS (this might change once we figure out certs...)
                                        var targetBinding = targetServiceDescription.Bindings.FirstOrDefault(b => b.Protocol == "http") ??
                                                            targetServiceDescription.Bindings.FirstOrDefault(b => b.Protocol == "https");

                                        if (targetBinding == null)
                                        {
                                            _logger.LogInformation("Service {ServiceName} does not have any HTTP or HTTPs bindings", targetServiceDescription.Name);
                                            continue;
                                        }

                                        // For each of the target service replicas, get the base URL
                                        // based on the replica port
                                        for (int i = 0; i < targetServiceDescription.Replicas; i++)
                                        {
                                            var port = targetBinding.ReplicaPorts[i];
                                            var url = $"{targetBinding.Protocol}://localhost:{port}";
                                            uris.Add((port, new Uri(url)));
                                        }

                                        _logger.LogInformation("Service {ServiceName} is using {Urls}", targetServiceDescription.Name, string.Join(",", uris.Select(u => u.ToString())));

                                        // The only load balancing strategy here is round robin
                                        long count = 0;
                                        RequestDelegate del = async context =>
                                        {
                                            var next = (int)(Interlocked.Increment(ref count) % uris.Count);

                                            // we find the first `Ready` port
                                            for (int i = 0; i < uris.Count; i++)
                                            {
                                                if (_readyPorts.ContainsKey(uris[next].Port))
                                                {
                                                    break;
                                                }

                                                next = (int)(Interlocked.Increment(ref count) % uris.Count);
                                            }

                                            // if we've looped through all the port and didn't find a single one that is `Ready`, we return HTTP BadGateway
                                            if (!_readyPorts.ContainsKey(uris[next].Port))
                                            {
                                                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                                                await context.Response.WriteAsync("Bad gateway");
                                                return;
                                            }
                                            var uri = new UriBuilder(uris[next].Uri)
                                            {
                                                Path = rule.PreservePath ? $"{context.Request.Path}" : (string?)context.Request.RouteValues["path"] ?? "/",
                                                Query = context.Request.QueryString.Value
                                            };

                                            await context.ProxyRequest(invoker, uri.Uri);
                                        };

                                        IEndpointConventionBuilder conventions =
                                            endpointBuilder.Map((rule.Path?.TrimEnd('/') ?? "") + "/{**path}", del);

                                        if (rule.Host != null)
                                        {
                                            conventions.WithMetadata(new IngressHostMetadata(rule.Host));
                                        }

                                        conventions.WithDisplayName(rule.Service);
                                    }
                                });
                            });
                        });


                    var webApp = host.Build();

                    _webApplications.Add(webApp);

                    // For each ingress rule, bind to the path and host

                    await webApp.StartAsync();

                    foreach (var replica in service.Replicas)
                    {
                        service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Started, replica.Value));
                    }
                }
            }
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

            foreach (var webApp in _webApplications)
            {
                try
                {
                    await webApp.StopAsync();
                }
                catch (OperationCanceledException)
                {

                }
                finally
                {
                    webApp.Dispose();
                }
            }
        }

        private void RegisterListener(Service service)
        {
            if (!service.Items.ContainsKey(typeof(Subscription)))
            {
                service.Items[typeof(Subscription)] = service.ReplicaEvents.Subscribe(OnReplicaEvent);
            }
        }

        private void OnReplicaEvent(ReplicaEvent replicaEvent)
        {
            foreach (var binding in replicaEvent.Replica.Bindings)
            {
                if (replicaEvent.State == ReplicaState.Ready)
                {
                    _readyPorts.TryAdd(binding.Port, true);
                }
                else
                {
                    _readyPorts.TryRemove(binding.Port, out _);
                }
            }
        }

        private class Subscription
        {
        }
    }
}
