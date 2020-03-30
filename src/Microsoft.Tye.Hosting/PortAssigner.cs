// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Model;
using Microsoft.Extensions.Logging;

namespace Microsoft.Tye.Hosting
{
    public class PortAssigner : IApplicationProcessor
    {
        private readonly ILogger _logger;

        public PortAssigner(ILogger logger)
        {
            _logger = logger;
        }

        public Task StartAsync(Application application)
        {
            foreach (var service in application.Services.Values)
            {
                if (service.Description.RunInfo == null)
                {
                    continue;
                }

                static int GetNextPort()
                {
                    // Let the OS assign the next available port. Unless we cycle through all ports
                    // on a test run, the OS will always increment the port number when making these calls.
                    // This prevents races in parallel test runs where a test is already bound to
                    // a given port, and a new test is able to bind to the same port due to port
                    // reuse being enabled by default by the OS.
                    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                    return ((IPEndPoint)socket.LocalEndPoint).Port;
                }

                foreach (var binding in service.Description.Bindings)
                {
                    // Auto assign ports if there's no connection string and no port
                    if (binding.Port != null || binding.ConnectionString == null)
                    {
                        continue;
                    }

                    // Auto assign a port
                    if (binding.Port == null)
                    {
                        binding.Port = GetNextPort();
                    }

                    if (service.Description.Replicas == 1)
                    {
                        // No need to proxy, the port maps to itself
                        binding.ReplicaPorts.Add(binding.Port.Value);
                        continue;
                    }

                    for (var i = 0; i < service.Description.Replicas; i++)
                    {
                        // Reserve a port for each replica
                        var port = GetNextPort();
                        binding.ReplicaPorts.Add(port);
                    }

                    _logger.LogInformation(
                        "Mapping external port {ExternalPort} to internal port(s) {InternalPorts} for {ServiceName} binding {BindingName}",
                        binding.Port,
                        string.Join(", ", binding.ReplicaPorts.Select(p => p.ToString())),
                        service.Description.Name,
                        binding.Name ?? binding.Protocol);
                }

                var httpBinding = service.Description.Bindings.FirstOrDefault(b => b.Protocol == "http");
                var httpsBinding = service.Description.Bindings.FirstOrDefault(b => b.Protocol == "https");

                // Default the first http and https port to 80 and 443
                if (httpBinding != null)
                {
                    httpBinding.ContainerPort = 80;
                }

                if (httpsBinding != null)
                {
                    httpsBinding.ContainerPort = 443;
                }

            }

            return Task.CompletedTask;
        }

        public Task StopAsync(Application application)
        {
            return Task.CompletedTask;
        }
    }
}
