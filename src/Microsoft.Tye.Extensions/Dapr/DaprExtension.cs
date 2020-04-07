// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Extensions.Dapr
{
    internal sealed class DaprExtension : Extension
    {
        public override string Name => "dapr";

        public override Task ProcessAsync(ExtensionContext context, ExtensionConfiguration config)
        {
            // If we're getting called then the user configured dapr in their tye.yaml.
            // We don't have any of our own config.

            if (context.Operation == ExtensionContext.OperationKind.LocalRun)
            {
                // For local run, enumerate all projects, and add services for each dapr proxy.
                var projects = context.Application.Services.OfType<ProjectServiceBuilder>().ToList();
                foreach (var project in projects)
                {
                    // Dapr requires http. If this project isn't listening to HTTP then it's not daprized.
                    var httpBinding = project.Bindings.Where(b => b.Protocol == "http").FirstOrDefault();
                    if (httpBinding == null)
                    {
                        continue;
                    }

                    var proxy = new ExecutableServiceBuilder($"{project.Name}-dapr", "daprd")
                    {
                        WorkingDirectory = context.Application.Source.DirectoryName,

                        // These environment variables are replaced with environment variables
                        // defined for this service.
                        Args = $"-app-id {project.Name} -app-port %APP_PORT% -dapr-grpc-port %DAPR_GRPC_PORT% --dapr-http-port %DAPR_HTTP_PORT% --metrics-port %METRICS_PORT% --placement-address localhost:50005",
                    };
                    context.Application.Services.Add(proxy);

                    // Listen for grpc on an auto-assigned port
                    var grpc = new BindingBuilder()
                    {
                        AutoAssignPort = true,
                        Name = "grpc",
                        Protocol = "https",
                    };
                    proxy.Bindings.Add(grpc);

                    // Listen for http on an auto-assigned port
                    var http = new BindingBuilder()
                    {
                        AutoAssignPort = true,
                        Name = "http",
                        Protocol = "http",
                    };
                    proxy.Bindings.Add(http);

                    // Listen for metrics on an auto-assigned port
                    var metrics = new BindingBuilder()
                    {
                        AutoAssignPort = true,
                        Name = "metrics",
                        Protocol = "http",
                    };
                    proxy.Bindings.Add(metrics);

                    // Set APP_PORT based on the project's assigned port for http
                    var appPort = new EnvironmentVariableBuilder("APP_PORT")
                    {
                        Source = new EnvironmentVariableSourceBuilder(project.Name, binding: httpBinding.Name)
                        {
                            Kind = EnvironmentVariableSourceBuilder.SourceKind.Port,
                        },
                    };
                    proxy.EnvironmentVariables.Add(appPort);

                    // Set DAPR_GRPC_PORT based on this service's assigned port
                    var daprGrpcPort = new EnvironmentVariableBuilder("DAPR_GRPC_PORT")
                    {
                        Source = new EnvironmentVariableSourceBuilder(proxy.Name, binding: "grpc")
                        {
                            Kind = EnvironmentVariableSourceBuilder.SourceKind.Port,
                        },
                    };
                    proxy.EnvironmentVariables.Add(daprGrpcPort);

                    // Add another copy of this envvar to the project.
                    daprGrpcPort = new EnvironmentVariableBuilder("DAPR_GRPC_PORT")
                    {
                        Source = new EnvironmentVariableSourceBuilder(proxy.Name, binding: "grpc")
                        {
                            Kind = EnvironmentVariableSourceBuilder.SourceKind.Port,
                        },
                    };
                    project.EnvironmentVariables.Add(daprGrpcPort);

                    // Set DAPR_Http_PORT based on this service's assigned port
                    var daprHttpPort = new EnvironmentVariableBuilder("DAPR_HTTP_PORT")
                    {
                        Source = new EnvironmentVariableSourceBuilder(proxy.Name, binding: "http")
                        {
                            Kind = EnvironmentVariableSourceBuilder.SourceKind.Port,
                        },
                    };
                    proxy.EnvironmentVariables.Add(daprHttpPort);

                    // Set METRICS_PORT to a random port
                    var metricsPort = new EnvironmentVariableBuilder("METRICS_PORT")
                    {
                        Source = new EnvironmentVariableSourceBuilder(proxy.Name, binding: "metrics")
                        {
                            Kind = EnvironmentVariableSourceBuilder.SourceKind.Port,
                        },
                    };
                    proxy.EnvironmentVariables.Add(metricsPort);
                }
            }
            else
            {
                // In deployment, enumerate all projects and add anotations to each one.
                var projects = context.Application.Services.OfType<ProjectServiceBuilder>();
                foreach (var project in projects)
                {
                    // Dapr requires http. If this project isn't listening to HTTP then it's not daprized.
                    var httpBinding = project.Bindings.Where(b => b.Protocol == "http").FirstOrDefault();
                    if (httpBinding == null)
                    {
                        continue;
                    }

                    if (project.ManifestInfo?.Deployment is DeploymentManifestInfo deployment)
                    {
                        deployment.Annotations.Add("dapr.io/enabled", "true");
                        deployment.Annotations.Add("dapr.io/id", project.Name);
                        deployment.Annotations.Add("dapr.io/port", (httpBinding.Port ?? 80).ToString(CultureInfo.InvariantCulture));

                        if (config.Data.TryGetValue("config", out var daprConfig) && daprConfig is object)
                        {
                            deployment.Annotations.TryAdd("dapr.io/config", daprConfig?.ToString());
                        }

                        if (config.Data.TryGetValue("log-level", out var logLevel) && logLevel is object)
                        {
                            deployment.Annotations.TryAdd("dapr.io/log-level", logLevel?.ToString());
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
