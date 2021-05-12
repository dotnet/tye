// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Extensions.Seq
{
    internal sealed class SeqExtension : Extension
    {
        public override Task ProcessAsync(ExtensionContext context, ExtensionConfiguration config)
        {
            string loggerProvider;
            var seqService = context.Application.Services.FirstOrDefault(x => x.Name == "seq");
            if (seqService != null)
            {
                context.Output.WriteDebugLine("seq service already configured. Skipping...");
                if (seqService.Bindings.Count == 1)
                {
                    var bindings = seqService.Bindings.First();
                    loggerProvider = $"seq={bindings.Protocol}://{bindings.Host}:{bindings.Port}";
                }
                else
                {
                    var ingestionBinding = seqService.Bindings.FirstOrDefault(x => x.Name == "ingestion");
                    if (ingestionBinding != null)
                    {
                        loggerProvider =
                            $"seq={ingestionBinding.Protocol}://{ingestionBinding.Host}:{ingestionBinding.Port}";
                    }
                    else
                    {
                        var bindings = seqService.Bindings.First();
                        loggerProvider = $"seq={bindings.Protocol}://{bindings.Host}:{bindings.Port}";
                    }
                }

                context.Output.WriteDebugLine($"get seq logger provider url from existing service: {loggerProvider}");
            }
            else
            {
                context.Output.WriteDebugLine("Injecting seq service...");

                var port = config.Data.TryGetValue("port", out var portValue) &&
                           portValue is string portString &&
                           int.TryParse(portString, out var value)
                    ? value
                    : 5341;

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
                            Port = port,
                            ContainerPort = 80,
                            Protocol = "http",
                        },
                    },
                };
                context.Application.Services.Add(seq);

                loggerProvider = $"seq=http://localhost:{port}";

                if (config.Data.TryGetValue("logPath", out var obj) &&
                    obj is string logPath &&
                    !string.IsNullOrEmpty(logPath))
                {
                    seq.Volumes.Add(new VolumeBuilder(logPath, "seq-data", "/data"));
                }
                else if (config.Data.TryGetValue("name", out var nameValue) &&
                         nameValue is string name &&
                         !string.IsNullOrEmpty(name))
                {
                    seq.Volumes.Add(new VolumeBuilder(null, name, "/data"));
                }

                foreach (var serviceBuilder in context.Application.Services)
                {
                    if (ReferenceEquals(serviceBuilder, seq))
                    {
                        continue;
                    }

                    // make seq available as a dependency of everything.
                    if (!serviceBuilder.Dependencies.Contains(seq.Name))
                    {
                        serviceBuilder.Dependencies.Add(seq.Name);
                    }
                }
            }

            switch (context.Operation)
            {
                case ExtensionContext.OperationKind.LocalRun:
                    {
                        if (context.Options!.LoggingProvider is null)
                        {
                            // For local development we hardcode the port and hostname
                            context.Options.LoggingProvider = loggerProvider;
                        }

                        break;
                    }
                case ExtensionContext.OperationKind.Deploy:
                    {
                        foreach (var project in context.Application.Services.OfType<DotnetProjectServiceBuilder>())
                        {
                            var sidecar = DiagnosticAgent.GetOrAddSidecar(project);

                            // Use service discovery to find seq
                            sidecar.Args.Add("--provider:seq=service:seq");
                            sidecar.Dependencies.Add("seq");
                        }

                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return Task.CompletedTask;
        }
    }
}
