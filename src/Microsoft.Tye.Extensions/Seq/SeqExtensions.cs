// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Extensions.Seq
{
    internal sealed class SeqExtension : Extension
    {
        public override Task ProcessAsync(ExtensionContext context, ExtensionConfiguration config)
        {
            if (context.Application.Services.Any(s => s.Name == "seq"))
            {
                context.Output.WriteDebugLine("seq service already configured. Skipping...");
            }
            else
            {
                context.Output.WriteDebugLine("Injecting seq service...");

                var seq = new ContainerServiceBuilder("seq", "datalust/seq")
                {
                    EnvironmentVariables = 
                    { 
                        new EnvironmentVariableBuilder("ACCEPT_EULA")
                        {
                            Value = "Y"
                        },
                    },
                    Bindings =
                    {
                        new BindingBuilder()
                        {
                            Name = "seq",
                            Port = 5340,
                            ContainerPort = 80,
                            Protocol = "http",
                        },
                    },
                };
                context.Application.Services.Add(seq);

                if (config.Data.TryGetValue("logPath", out var obj) &&
                    obj is string logPath &&
                    !string.IsNullOrEmpty(logPath))
                {
                    seq.Volumes.Add(new VolumeBuilder(logPath, "seq-data", "/data"));
                }

                foreach (var s in context.Application.Services)
                {
                    if (object.ReferenceEquals(s, seq))
                    {
                        continue;
                    }

                    // make seq available as a dependency of everything.
                    if (!s.Dependencies.Contains(seq.Name))
                    {
                        s.Dependencies.Add(seq.Name);
                    }
                }
            }

            if (context.Operation == ExtensionContext.OperationKind.LocalRun)
            {
                if (context.Options!.LoggingProvider is null)
                {
                    // For local development we hardcode the port and hostname
                    context.Options.LoggingProvider = "seq=http://localhost:5340";
                }
            }
            else if (context.Operation == ExtensionContext.OperationKind.Deploy)
            {
                foreach (var project in context.Application.Services.OfType<DotnetProjectServiceBuilder>())
                {
                    var sidecar = DiagnosticAgent.GetOrAddSidecar(project);

                    // Use service discovery to find seq
                    sidecar.Args.Add("--provider:seq=service:seq");
                    sidecar.Dependencies.Add("seq");
                }
            }

            return Task.CompletedTask;
        }
    }
}
