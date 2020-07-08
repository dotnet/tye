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

        public async Task StartAsync(Application application)
        {
            // rootless podman doesn't permit creation of networks.
            // Use the host network instead, and perform communication between applications using "localhost".
            if (string.IsNullOrEmpty(application.Network))
            {
                bool isPodman = await DockerDetector.Instance.IsPodman.Value;
                application.UseHostNetwork = isPodman;
            }

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
                    // We assign a port to each binding.
                    // When we use the host network, port mapping is not supported.
                    // The ContainerPort and Port need to match.
                    if (binding.Port == null)
                    {
                        // UseHostNetwork: ContainerPort exposes the service on localhost
                        //                 Set Port to match ContainerPort.
                        if (application.UseHostNetwork && binding.ContainerPort.HasValue)
                        {
                            binding.Port = binding.ContainerPort.Value;
                        }
                        else
                        {
                            // Pick a random port.
                            binding.Port = GetNextPort();
                        }
                    }

                    if (service.Description.Readiness == null && service.Description.Replicas == 1)
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

                // Set ContainerPort for the first http and https port.
                // For ASP.NET we'll match the Port when UseHostNetwork. ASPNETCORE_URLS will configure the application.
                // For other applications, we use the default ports 80 and 443.
                var httpBinding = service.Description.Bindings.FirstOrDefault(b => b.Protocol == "http");
                var httpsBinding = service.Description.Bindings.FirstOrDefault(b => b.Protocol == "https");
                bool isAspNetWithHostNetwork = application.UseHostNetwork &&
                                               (service.Description.RunInfo as DockerRunInfo)?.IsAspNet == true;
                if (httpBinding != null)
                {
                    httpBinding.ContainerPort ??= isAspNetWithHostNetwork ? httpBinding.Port : 80;
                }

                if (httpsBinding != null)
                {
                    httpsBinding.ContainerPort ??= isAspNetWithHostNetwork ? httpsBinding.Port : 443;
                }
            }
        }

        public Task StopAsync(Application application)
        {
            return Task.CompletedTask;
        }
    }
}
