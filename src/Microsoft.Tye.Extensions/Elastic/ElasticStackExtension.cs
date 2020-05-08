// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Extensions.Elastic
{
    internal sealed class ElasticStackExtension : Extension
    {
        public override Task ProcessAsync(ExtensionContext context, ExtensionConfiguration config)
        {
            if (context.Application.Services.Any(s => s.Name == "elastic"))
            {
                context.Output.WriteDebugLine("elastic service already configured. Skipping...");
            }
            else
            {
                context.Output.WriteDebugLine("Injecting elastic service...");

                // We're using an "all-in-one" docker image for local dev that makes that
                // easy to set up.
                //
                // See: https://elk-docker.readthedocs.io/
                var elastic = new ContainerServiceBuilder("elastic", "sebp/elk")
                {
                    Bindings =
                    {
                        new BindingBuilder()
                        {
                            Name = "kibana",
                            Port = 5601,
                            ContainerPort = 5601,
                            Protocol = "http",
                        },
                        new BindingBuilder()
                        {
                            Port = 9200,
                            ContainerPort = 9200,
                            Protocol = "http",
                        },
                    },
                };
                context.Application.Services.Add(elastic);

                if (config.Data.TryGetValue("logPath", out var obj) &&
                    obj is string logPath &&
                    !string.IsNullOrEmpty(logPath))
                {
                    // https://elk-docker.readthedocs.io/#persisting-log-data
                    elastic.Volumes.Add(new VolumeBuilder(logPath, "elk-data", "/var/lib/elasticsearch"));
                }

                foreach (var s in context.Application.Services)
                {
                    if (object.ReferenceEquals(s, elastic))
                    {
                        continue;
                    }

                    // make elastic available as a dependency of everything.
                    if (!s.Dependencies.Contains(elastic.Name))
                    {
                        s.Dependencies.Add(elastic.Name);
                    }
                }
            }

            if (context.Operation == ExtensionContext.OperationKind.LocalRun)
            {
                if (context.Options!.LoggingProvider is null)
                {
                    // For local development we hardcode the port and hostname
                    context.Options.LoggingProvider = "elastic=http://localhost:9200";
                }
            }
            else if (context.Operation == ExtensionContext.OperationKind.Deploy)
            {
                // For deployments, remove the kibana binding. We don't need to talk to it,
                // so don't make the user specify it.
                var elastic = context.Application.Services.Single(s => s.Name == "elastic");
                var kibana = elastic.Bindings.Single(b => b.Name == "kibana");
                elastic.Bindings.Remove(kibana);

                foreach (var project in context.Application.Services.OfType<ProjectServiceBuilder>())
                {
                    var sidecar = DiagnosticAgent.GetOrAddSidecar(project);

                    // Use service discovery to find elastic
                    sidecar.Args.Add("--provider:elastic=service:elastic");
                    sidecar.Dependencies.Add("elastic");
                }
            }

            return Task.CompletedTask;
        }
    }
}
