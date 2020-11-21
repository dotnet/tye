// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Tye.Hosting.Model;
using Microsoft.Tye.Hosting.Model.V1;

namespace Microsoft.Tye.Hosting
{
    public class TyeDashboardApi
    {
        private readonly JsonSerializerOptions _options;

        public TyeDashboardApi()
        {
            _options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            };

            _options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        public void MapRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/v1", ServiceIndex);
            endpoints.MapGet("/api/v1/services", Services);
            endpoints.MapGet("/api/v1/services/{name}", Service);
            endpoints.MapGet("/api/v1/logs/{name}", Logs);
            endpoints.MapGet("/api/v1/metrics", AllMetrics);
            endpoints.MapGet("/api/v1/metrics/{name}", Metrics);
        }

        private Task ServiceIndex(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            return JsonSerializer.SerializeAsync(context.Response.Body, new[]
            {
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/services",
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/logs/{{service}}",
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/metrics",
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/metrics/{{service}}",
            },
            _options);
        }

        private Task Services(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            context.Response.ContentType = "application/json";

            var services = app.Services.OrderBy(s => s.Key).Select(s => s.Value);

            var list = new List<V1Service>();
            foreach (var service in services)
            {
                list.Add(CreateServiceJson(service));
            }

            return JsonSerializer.SerializeAsync(context.Response.Body, list, _options);
        }

        private Task Service(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            var name = (string?)context.Request.RouteValues["name"];
            context.Response.ContentType = "application/json";

            if (string.IsNullOrEmpty(name) || !app.Services.TryGetValue(name, out var service))
            {
                context.Response.StatusCode = 404;
                return JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    message = $"Unknown service {name}"
                },
                _options);
            }

            var serviceJson = CreateServiceJson(service);

            return JsonSerializer.SerializeAsync(context.Response.Body, serviceJson, _options);
        }

        private static V1Service CreateServiceJson(Service service)
        {
            var description = service.Description;
            var bindings = description.Bindings;

            var v1BindingList = bindings.Select(binding => new V1ServiceBinding()
            {
                Name = binding.Name,
                ConnectionString = binding.ConnectionString,
                Port = binding.Port,
                ContainerPort = binding.ContainerPort,
                Host = binding.Host,
                Protocol = binding.Protocol
            }).ToList();

            var v1ConfigurationSourceList = new List<V1ConfigurationSource>();
            foreach (var (name, value) in description.Configuration)
            {
                v1ConfigurationSourceList.Add(new V1ConfigurationSource()
                {
                    Name = name,
                    Value = value
                });
            }

            var v1RunInfo = new V1RunInfo();
            if (description.RunInfo is DockerRunInfo dockerRunInfo)
            {
                v1RunInfo.Type = V1RunInfoType.Docker;
                v1RunInfo.Image = dockerRunInfo.Image;
                v1RunInfo.VolumeMappings = dockerRunInfo.VolumeMappings.Select(v => new V1DockerVolume
                {
                    Name = v.Name,
                    Source = v.Source,
                    Target = v.Target
                }).ToList();

                v1RunInfo.WorkingDirectory = dockerRunInfo.WorkingDirectory;
                v1RunInfo.Args = dockerRunInfo.Args;
            }
            else if (description.RunInfo is ExecutableRunInfo executableRunInfo)
            {
                v1RunInfo.Type = V1RunInfoType.Executable;
                v1RunInfo.Args = executableRunInfo.Args;
                v1RunInfo.Executable = executableRunInfo.Executable;
                v1RunInfo.WorkingDirectory = executableRunInfo.WorkingDirectory;
            }
            else if (description.RunInfo is ProjectRunInfo projectRunInfo)
            {
                v1RunInfo.Type = V1RunInfoType.Project;
                v1RunInfo.Args = projectRunInfo.Args;
                v1RunInfo.Build = projectRunInfo.Build;
                v1RunInfo.Project = projectRunInfo.ProjectFile.FullName;
            }

            var v1ServiceDescription = new V1ServiceDescription()
            {
                Bindings = v1BindingList,
                Configuration = v1ConfigurationSourceList,
                Name = description.Name,
                Replicas = description.Replicas,
                RunInfo = v1RunInfo
            };

            var replicateDictionary = new Dictionary<string, V1ReplicaStatus>();
            foreach (var replica in service.Replicas)
            {
                var replicaStatus = new V1ReplicaStatus()
                {
                    Name = replica.Value.Name,
                    Ports = replica.Value.Ports,
                    Environment = replica.Value.Environment,
                    State = replica.Value.State
                };

                replicateDictionary[replica.Key] = replicaStatus;

                if (replica.Value is ProcessStatus processStatus)
                {
                    replicaStatus.Pid = processStatus.Pid;
                    replicaStatus.ExitCode = processStatus.ExitCode;
                }
                else if (replica.Value is DockerStatus dockerStatus)
                {
                    replicaStatus.DockerCommand = dockerStatus.DockerCommand;
                    replicaStatus.ContainerId = dockerStatus.ContainerId;
                    replicaStatus.DockerNetwork = dockerStatus.DockerNetwork;
                    replicaStatus.DockerNetworkAlias = dockerStatus.DockerNetworkAlias;
                }
            }

            var v1Status = new V1ServiceStatus()
            {
                ProjectFilePath = service.Status.ProjectFilePath,
                ExecutablePath = service.Status.ExecutablePath,
                Args = service.Status.Args,
                WorkingDirectory = service.Status.WorkingDirectory,
            };

            var serviceJson = new V1Service()
            {
                ServiceType = service.ServiceType,
                Status = v1Status,
                Description = v1ServiceDescription,
                Replicas = replicateDictionary,
                Restarts = service.Restarts
            };

            return serviceJson;
        }

        private Task Logs(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Tye.Hosting.Model.Application>();

            var name = (string?)context.Request.RouteValues["name"];
            context.Response.ContentType = "application/json";

            if (string.IsNullOrEmpty(name) || !app.Services.TryGetValue(name, out var service))
            {
                context.Response.StatusCode = 404;
                return JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    message = $"Unknown service {name}"
                },
                _options);
            }

            return JsonSerializer.SerializeAsync(context.Response.Body, service.CachedLogs, _options);
        }

        private Task AllMetrics(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Tye.Hosting.Model.Application>();

            var sb = new StringBuilder();
            foreach (var s in app.Services.OrderBy(s => s.Key))
            {
                sb.AppendLine($"# {s.Key}");
                foreach (var replica in s.Value.Replicas)
                {
                    foreach (var metric in replica.Value.Metrics)
                    {
                        sb.Append(metric.Key);
                        sb.Append("{");
                        sb.Append($"service=\"{s.Key}\",");
                        sb.Append($"instance=\"{replica.Key}\"");
                        sb.Append("}");
                        sb.Append(" ");
                        sb.Append(metric.Value);
                        sb.AppendLine();
                    }
                }
                sb.AppendLine();
            }

            return context.Response.WriteAsync(sb.ToString());
        }

        private Task Metrics(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            var sb = new StringBuilder();

            var name = (string?)context.Request.RouteValues["name"];
            context.Response.ContentType = "application/json";

            if (string.IsNullOrEmpty(name) || !app.Services.TryGetValue(name, out var service))
            {
                context.Response.StatusCode = 404;
                return JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    message = $"Unknown service {name}"
                },
                _options);
            }

            foreach (var replica in service.Replicas)
            {
                foreach (var metric in replica.Value.Metrics)
                {
                    sb.Append(metric.Key);
                    sb.Append("{");
                    sb.Append($"instance=\"{replica.Key}\"");
                    sb.Append("}");
                    sb.Append(" ");
                    sb.Append(metric.Value);
                    sb.AppendLine();
                }
            }

            return context.Response.WriteAsync(sb.ToString());
        }
    }
}
