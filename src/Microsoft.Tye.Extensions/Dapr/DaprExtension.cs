// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.Tye.Extensions.Dapr
{
    internal sealed class DaprExtension : Extension
    {
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
                    var httpBinding = project.Bindings.FirstOrDefault(b => b.Protocol == "http");
                    if (httpBinding == null)
                    {
                        continue;
                    }

                    // See https://github.com/dotnet/tye/issues/260
                    //
                    // Currently the pub-sub pattern does not work when you have multiple replicas. Each
                    // daprd instance expects that it has a single application to talk to. So if you're using
                    // pub-sub this means that you'll won't get some messages.
                    //
                    // We have no way to know if an app is using pub-sub or not, so just block it.
                    if (project.Replicas > 1)
                    {
                        throw new CommandException("Dapr support does not support multiple replicas yet for development.");
                    }

                    var daprExecutablePath = GetDaprExecutablePath();

                    var proxy = new ExecutableServiceBuilder($"{project.Name}-dapr", daprExecutablePath)
                    {
                        WorkingDirectory = context.Application.Source.DirectoryName,

                        // These environment variables are replaced with environment variables
                        // defined for this service.
                        Args = $"-app-id {project.Name} -app-port %APP_PORT% -dapr-grpc-port %DAPR_GRPC_PORT% --dapr-http-port %DAPR_HTTP_PORT% --metrics-port %METRICS_PORT% --placement-address localhost:50005",
                    };

                    // When running locally `-config` specifies a filename, not a configuration name. By convention
                    // we'll assume the filename and config name are the same.
                    if (config.Data.TryGetValue("config", out var obj) && obj?.ToString() is string daprConfig)
                    {
                        var configFile = Path.Combine(context.Application.Source.DirectoryName!, "components", $"{daprConfig}.yaml");
                        if (File.Exists(configFile))
                        {
                            proxy.Args += $" -config \"{configFile}\"";
                        }
                        else
                        {
                            context.Output.WriteAlwaysLine($"Could not find dapr config '{configFile}'. Skipping...");
                        }
                    }

                    if (config.Data.TryGetValue("log-level", out obj) && obj?.ToString() is string logLevel)
                    {
                        proxy.Args += $" -log-level {logLevel}";
                    }

                    if (config.Data.TryGetValue("components-path", out obj) && obj?.ToString() is string componentsPath)
                    {
                        proxy.Args += $" -components-path {componentsPath}";
                    }
                    // Add dapr proxy as a service available to everyone.
                    proxy.Dependencies.UnionWith(context.Application.Services.Select(s => s.Name));

                    foreach (var s in context.Application.Services)
                    {
                        s.Dependencies.Add(proxy.Name);
                    }

                    context.Application.Services.Add(proxy);

                    // Listen for grpc on an auto-assigned port
                    var grpc = new BindingBuilder()
                    {
                        Name = "grpc",
                        Protocol = "https",
                    };
                    proxy.Bindings.Add(grpc);

                    // Listen for http on an auto-assigned port
                    var http = new BindingBuilder()
                    {
                        Name = "http",
                        Protocol = "http",
                    };
                    proxy.Bindings.Add(http);

                    // Listen for metrics on an auto-assigned port
                    var metrics = new BindingBuilder()
                    {
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

                    // Set DAPR_HTTP_PORT based on this service's assigned port
                    var daprHttpPort = new EnvironmentVariableBuilder("DAPR_HTTP_PORT")
                    {
                        Source = new EnvironmentVariableSourceBuilder(proxy.Name, binding: "http")
                        {
                            Kind = EnvironmentVariableSourceBuilder.SourceKind.Port,
                        },
                    };
                    proxy.EnvironmentVariables.Add(daprHttpPort);

                    // Add another copy of this envvar to the project.
                    daprHttpPort = new EnvironmentVariableBuilder("DAPR_HTTP_PORT")
                    {
                        Source = new EnvironmentVariableSourceBuilder(proxy.Name, binding: "http")
                        {
                            Kind = EnvironmentVariableSourceBuilder.SourceKind.Port,
                        },
                    };
                    project.EnvironmentVariables.Add(daprHttpPort);

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

                        if (config.Data.TryGetValue("config", out var obj) && obj?.ToString() is string daprConfig)
                        {
                            deployment.Annotations.TryAdd("dapr.io/config", daprConfig);
                        }

                        if (config.Data.TryGetValue("log-level", out obj) && obj?.ToString() is string logLevel)
                        {
                            deployment.Annotations.TryAdd("dapr.io/log-level", logLevel);
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        private string GetDaprExecutablePath()
        {
            // Starting with dapr version 11, dapr is installed in user profile/home.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var windowsPath = Environment.ExpandEnvironmentVariables("%USERPROFILE%/.dapr/bin/daprd.exe");
                if (File.Exists(windowsPath))
                {
                    return windowsPath;
                }
            }
            else
            {
                var nixpath = Environment.ExpandEnvironmentVariables("$HOME/.dapr/bin/daprd");
                if (File.Exists(nixpath))
                {
                    return nixpath;
                }
            }

            // Older version of dapr don't have dapr in the bin directory, but it is usually on the path.
            return "daprd";
        }
    }
}
