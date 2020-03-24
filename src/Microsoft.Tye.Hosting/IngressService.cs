// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Proxy;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class IngressService : IApplicationProcessor
    {
        private List<WebApplication> _webApplications = new List<WebApplication>();
        private readonly ILogger _logger;

        public IngressService(ILogger logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(Model.Application application)
        {
            var invoker = new HttpMessageInvoker(new ConnectionRetryHandler(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseProxy = false
            }));

            foreach (var service in application.Services.Values)
            {
                var serviceDescription = service.Description;

                if (service.Description.RunInfo is IngressRunInfo runInfo)
                {
                    var builder = new WebApplicationBuilder();

                    builder.Services.AddSingleton<MatcherPolicy, IngressHostMatcherPolicy>();

                    builder.Logging.AddProvider(new ServiceLoggerProvider(service.Logs));

                    var addresses = new List<string>();

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

                            var port = service.PortMap[binding.Port.Value][i];
                            ports.Add(port);
                            var url = $"{binding.Protocol ?? "http"}://localhost:{port}";
                            addresses.Add(url);
                        }

                        status.Ports = ports;

                        service.ReplicaEvents.OnNext(new ReplicaEvent(ReplicaState.Added, status));
                    }

                    builder.Server.UseUrls(addresses.ToArray());
                    var webApp = builder.Build();

                    _webApplications.Add(webApp);

                    // For each ingress rule, bind to the path and host
                    foreach (var rule in runInfo.Rules)
                    {
                        if (!application.Services.TryGetValue(rule.Service, out var target))
                        {
                            continue;
                        }

                        _logger.LogInformation("Processing ingress rule: Path:{Path}, Host:{Host}, Service:{Service}", rule.Path, rule.Host, rule.Service);

                        var targetServiceDescription = target.Description;

                        var uris = new List<Uri>();

                        // For each of the target service replicas, get the base URL
                        // based on the replica port
                        for (int i = 0; i < targetServiceDescription.Replicas; i++)
                        {
                            foreach (var binding in targetServiceDescription.Bindings)
                            {
                                if (binding.Port == null)
                                {
                                    continue;
                                }

                                var port = target.PortMap[binding.Port.Value][i];
                                var url = $"{binding.Protocol ?? "http"}://localhost:{port}";
                                uris.Add(new Uri(url));
                            }
                        }

                        // The only load balancing strategy here is round robin
                        long count = 0;
                        RequestDelegate del = context =>
                        {
                            var next = (int)(Interlocked.Increment(ref count) % uris.Count);

                            var uri = new UriBuilder(uris[next])
                            {
                                Path = (string)context.Request.RouteValues["path"]
                            };

                            return context.ProxyRequest(invoker, uri.Uri);
                        };

                        IEndpointConventionBuilder conventions = null!;

                        if (rule.Path != null)
                        {
                            conventions = ((IEndpointRouteBuilder)webApp).Map(rule.Path.TrimEnd('/') + "/{**path}", del);
                        }
                        else
                        {
                            conventions = webApp.MapFallback(del);
                        }

                        if (rule.Host != null)
                        {
                            conventions.WithMetadata(new IngressHostMetadata(rule.Host));
                        }

                        conventions.WithDisplayName(rule.Service);
                    }
                }
            }

            foreach (var app in _webApplications)
            {
                await app.StartAsync();
            }
        }

        public async Task StopAsync(Model.Application application)
        {
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

        private class ConnectionRetryHandler : DelegatingHandler
        {
            private static readonly int MaxRetries = 3;
            private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(1000);

            public ConnectionRetryHandler(HttpMessageHandler innerHandler)
                : base(innerHandler)
            {
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                HttpResponseMessage? response = null;
                var delay = InitialRetryDelay;
                Exception? exception = null;

                for (var i = 0; i < MaxRetries; i++)
                {
                    try
                    {
                        response = await base.SendAsync(request, cancellationToken);
                    }
                    catch (HttpRequestException ex) when (ex.InnerException is SocketException)
                    {
                        if (i == MaxRetries - 1)
                        {
                            throw;
                        }

                        exception = ex;
                    }

                    if (response != null &&
                       (response.IsSuccessStatusCode || response.StatusCode != HttpStatusCode.ServiceUnavailable))
                    {
                        return response;
                    }

                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                }

                if (exception != null)
                {
                    ExceptionDispatchInfo.Throw(exception);
                }

                throw new TimeoutException();
            }
        }

        private class ServiceLoggerProvider : ILoggerProvider
        {
            private readonly Subject<string> _logs;

            public ServiceLoggerProvider(Subject<string> logs)
            {
                _logs = logs;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new ServiceLogger(categoryName, _logs);
            }

            public void Dispose()
            {
            }

            private class ServiceLogger : ILogger
            {
                private readonly string _categoryName;
                private readonly Subject<string> _logs;

                public ServiceLogger(string categoryName, Subject<string> logs)
                {
                    _categoryName = categoryName;
                    _logs = logs;
                }

                public IDisposable? BeginScope<TState>(TState state)
                {
                    return null;
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return true;
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    _logs.OnNext($"[{logLevel}]: {formatter(state, exception)}");

                    if (exception != null)
                    {
                        _logs.OnNext(exception.ToString());
                    }
                }
            }
        }
    }
}
