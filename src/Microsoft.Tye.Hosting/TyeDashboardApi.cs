// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;
using System.Collections.Generic;
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

        private async Task Services(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            context.Response.ContentType = "application/json";

            var services = app.Services.OrderBy(s => s.Key).Select(s => s.Value);

            var list = new List<V1Service>();
            foreach (var service in services)
            {
                list.Add(CreateServiceJson(service));
            }

            await JsonSerializer.SerializeAsync(context.Response.Body, list, _options);
        }

        private async Task Service(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            var name = (string)context.Request.RouteValues["name"];
            context.Response.ContentType = "application/json";

            if (!app.Services.TryGetValue(name, out var service))
            {
                context.Response.StatusCode = 404;
                await JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    message = $"Unknown service {name}"
                },
                _options);

                return;
            }

            var serviceJson = CreateServiceJson(service);

            await JsonSerializer.SerializeAsync(context.Response.Body, serviceJson, _options);
        }

        private static V1Service CreateServiceJson(Model.Service service)
        {
            var description = service.Description;
            var bindings = description.Bindings;

            var v1bindingList = new List<V1ServiceBinding>();

            foreach (var binding in bindings)
            {
                v1bindingList.Add(new V1ServiceBinding()
                {
                    Name = binding.Name,
                    ConnectionString = binding.ConnectionString,
                    AutoAssignPort = binding.AutoAssignPort,
                    Port = binding.Port,
                    ContainerPort = binding.ContainerPort,
                    Host = binding.Host,
                    Protocol = binding.Protocol
                });
            }

            var v1ConfigurationSourceList = new List<V1ConfigurationSource>();
            foreach (var configSource in description.Configuration)
            {
                v1ConfigurationSourceList.Add(new V1ConfigurationSource()
                {
                    Name = configSource.Name,
                    Value = configSource.Value
                });
            }

            var v1RunInfo = new V1RunInfo();
            if (description.RunInfo is DockerRunInfo dockerRunInfo)
            {
                v1RunInfo.Type = V1RunInfoType.Docker;
                v1RunInfo.Image = dockerRunInfo.Image;
                v1RunInfo.VolumeMappings = dockerRunInfo.VolumeMappings;
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
                Bindings = v1bindingList,
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
                };

                replicateDictionary[replica.Key] = replicaStatus;

                if (replica.Value is ProcessStatus processStatus)
                {
                    replicaStatus.Pid = processStatus.Pid;
                    replicaStatus.ExitCode = processStatus.ExitCode;
                    replicaStatus.Environment = processStatus.Environment;
                }
                else if (replica.Value is DockerStatus dockerStatus)
                {
                    replicaStatus.DockerCommand = dockerStatus.DockerCommand;
                    replicaStatus.DockerLogsPid = dockerStatus.DockerLogsPid;
                    replicaStatus.ContainerId = dockerStatus.ContainerId;
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

        private async Task Logs(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Tye.Hosting.Model.Application>();

            var name = (string)context.Request.RouteValues["name"];
            context.Response.ContentType = "application/json";

            if (!app.Services.TryGetValue(name, out var service))
            {
                context.Response.StatusCode = 404;
                await JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    message = $"Unknown service {name}"
                },
                _options);

                return;
            }

            await JsonSerializer.SerializeAsync(context.Response.Body, service.CachedLogs, _options);
        }

        private async Task AllMetrics(HttpContext context)
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

            await context.Response.WriteAsync(sb.ToString());
        }

        private async Task Metrics(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            var sb = new StringBuilder();

            var name = (string)context.Request.RouteValues["name"];
            context.Response.ContentType = "application/json";

            if (!app.Services.TryGetValue(name, out var service))
            {
                context.Response.StatusCode = 404;
                await JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    message = $"Unknown service {name}"
                },
                _options);

                return;
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

            await context.Response.WriteAsync(sb.ToString());
        }
    }
}
