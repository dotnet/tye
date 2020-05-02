// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Tye
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;
                    services.Configure<DiagnosticsMonitorOptions>(options =>
                    {
                        options.Kubernetes = string.Equals(bool.TrueString, configuration["kubernetes"], StringComparison.OrdinalIgnoreCase);
                        options.AssemblyName = configuration["assemblyName"];
                        options.Service = configuration["service"];

                        var section = configuration.GetSection("provider");
                        foreach (var child in section.GetChildren())
                        {
                            if (!DiagnosticsProvider.TryParse(child.Value, DiagnosticsProvider.ProviderKind.Unknown, out var provider))
                            {
                                throw new InvalidOperationException("Could not parse diagnostics provider: " + child.Value);
                            }

                            // The value used to connect to the service should be accessible with Tye service discovery.
                            if (provider.Value is string && provider.Value.StartsWith("service:"))
                            {
                                var service = provider.Value.Substring("service:".Length);
                                var value = configuration.GetServiceUri(service)?.AbsoluteUri ?? configuration.GetConnectionString(service);
                                provider = new DiagnosticsProvider(provider.Key, value, provider.Kind);
                            }

                            options.Providers.Add(provider);
                        }

                        if (options.Providers.Count == 0)
                        {
                            throw new InvalidOperationException("At least one provider must be configured.");
                        }

                        if (string.IsNullOrEmpty(options.AssemblyName))
                        {
                            throw new InvalidOperationException("The assembly name is required.");
                        }

                        if (string.IsNullOrEmpty(options.Service))
                        {
                            throw new InvalidOperationException("The service name is required.");
                        }
                    });
                    services.AddHostedService<DiagnosticsMonitor>();
                });
    }
}
