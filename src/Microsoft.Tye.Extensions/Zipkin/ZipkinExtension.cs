// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Extensions.Zipkin
{
    internal sealed class ZipkinExtension : Extension
    {
        public override Task ProcessAsync(ExtensionContext context, ExtensionConfiguration config)
        {
            if (context.Application.Services.Any(s => s.Name == "zipkin"))
            {
                context.Output.WriteDebugLine("zipkin service already configured. Skipping...");
            }
            else
            {
                context.Output.WriteDebugLine("Injecting zipkin service...");
                var service = new ContainerServiceBuilder("zipkin", "openzipkin/zipkin")
                {
                    Bindings =
                    {
                        new BindingBuilder()
                        {
                            Port = 9411,
                            ContainerPort = 9411,
                            Protocol = "http",
                        },
                    },
                };
                context.Application.Services.Add(service);

                foreach (var s in context.Application.Services)
                {
                    if (object.ReferenceEquals(s, service))
                    {
                        continue;
                    }

                    // make zipkin available as a dependency of everything.
                    if (!s.Dependencies.Contains(service.Name))
                    {
                        s.Dependencies.Add(service.Name);
                    }
                }
            }

            if (context.Operation == ExtensionContext.OperationKind.LocalRun)
            {
                if (context.Options!.DistributedTraceProvider is null)
                {
                    // For local development we hardcode the port and hostname
                    context.Options.DistributedTraceProvider = "zipkin=http://localhost:9411";
                }
            }
            else if (context.Operation == ExtensionContext.OperationKind.Deploy)
            {
                foreach (var project in context.Application.Services.OfType<DotnetProjectServiceBuilder>())
                {
                    var sidecar = DiagnosticAgent.GetOrAddSidecar(project);

                    // Use service discovery to find zipkin
                    sidecar.Args.Add("--provider:zipkin=service:zipkin");
                    sidecar.Dependencies.Add("zipkin");
                }
            }

            return Task.CompletedTask;
        }
    }
}
