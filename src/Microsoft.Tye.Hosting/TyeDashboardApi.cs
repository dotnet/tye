// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.Tye.Hosting.Model;
using Microsoft.Tye.Hosting.Model.V1;

namespace Microsoft.Tye.Hosting
{
    public class TyeDashboardApi
    {
        private readonly JsonSerializerOptions _options;
        private readonly ProcessRunner _processRunner;

        public TyeDashboardApi(ProcessRunner processRunner)
        {
            _options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            };

            _options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            _processRunner = processRunner;
        }

        public void MapRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/v1", ServiceIndex);
            endpoints.MapGet("/api/v1/application", ApplicationIndex);
            endpoints.MapDelete("/api/v1/control", ControlPlaneShutdown);
            endpoints.MapGet("/api/v1/services", Services);
            endpoints.MapPost("/api/v1/services/{name}/stop", ServiceStop);
            endpoints.MapPost("/api/v1/services/{name}/start", ServiceStart);
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
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/application",
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/control",
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/logs/{{service}}",
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/metrics",
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/metrics/{{service}}",
                $"{context.Request.Scheme}://{context.Request.Host}/api/v1/services",
            },
            _options);
        }

        private Task ApplicationIndex(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            context.Response.ContentType = "application/json";

            return JsonSerializer.SerializeAsync(
                context.Response.Body,
                new V1Application
                {
                    Id = app.Id,
                    Name = app.Name,
                    Source = app.Source
                },
                _options);
        }

        private Task ControlPlaneShutdown(HttpContext context)
        {
            var lifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();

            lifetime.StopApplication();

            return context.Response.CompleteAsync();
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

        private Task ServiceStart(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            var name = (string?)context.Request.RouteValues["name"];
            if (!string.IsNullOrEmpty(name) && app.Services.TryGetValue(name, out var service))
            {
                _processRunner.LaunchService(app, service);
            }

            context.Response.Redirect($"/services/{name}");

            return Task.CompletedTask;
        }

        private async Task ServiceStop(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            var name = (string?)context.Request.RouteValues["name"];
            if (!string.IsNullOrEmpty(name) && app.Services.TryGetValue(name, out var service))
            {
                var services = new Dictionary<string, Service>();
                services.Add(name, service);
                await _processRunner.KillRunningProcesses(services);
            }

            context.Response.Redirect($"/services/{name}");

            return;
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
            switch (description.RunInfo)
            {
                case DockerRunInfo dockerRunInfo:
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
                    break;
                case ExecutableRunInfo executableRunInfo:
                    v1RunInfo.Type = V1RunInfoType.Executable;
                    v1RunInfo.Args = executableRunInfo.Args;
                    v1RunInfo.Executable = executableRunInfo.Executable;
                    v1RunInfo.WorkingDirectory = executableRunInfo.WorkingDirectory;
                    break;
                case ProjectRunInfo projectRunInfo:
                    v1RunInfo.Type = V1RunInfoType.Project;
                    v1RunInfo.Args = projectRunInfo.Args;
                    v1RunInfo.Build = projectRunInfo.Build;
                    v1RunInfo.Project = projectRunInfo.ProjectFile.FullName;
                    break;
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
            foreach (var (instance, replica) in service.Replicas)
            {
                var replicaStatus = new V1ReplicaStatus()
                {
                    Name = replica.Name,
                    Ports = replica.Ports,
                    Environment = replica.Environment,
                    State = replica.State
                };

                replicateDictionary[instance] = replicaStatus;

                switch (replica)
                {
                    case ProcessStatus processStatus:
                        replicaStatus.Pid = processStatus.Pid;
                        replicaStatus.ExitCode = processStatus.ExitCode;
                        break;
                    case DockerStatus dockerStatus:
                        replicaStatus.DockerCommand = dockerStatus.DockerCommand;
                        replicaStatus.ContainerId = dockerStatus.ContainerId;
                        replicaStatus.DockerNetwork = dockerStatus.DockerNetwork;
                        replicaStatus.DockerNetworkAlias = dockerStatus.DockerNetworkAlias;
                        break;
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
                ServiceSource = service.ServiceSource,
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

            if (!string.IsNullOrEmpty(name) && app.Services.TryGetValue(name, out var service))
            {
                return JsonSerializer.SerializeAsync(context.Response.Body, service.CachedLogs, _options);
            }

            context.Response.StatusCode = 404;
            return JsonSerializer.SerializeAsync(context.Response.Body, new
            {
                message = $"Unknown service {name}"
            }, _options);
        }

        private Task AllMetrics(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            var acceptHeader = context.Request.Headers[HeaderNames.Accept];

            if (acceptHeader == MediaTypeNames.Application.Json)
            {
                context.Response.ContentType = MediaTypeNames.Application.Json;
                var servicesMetricsCollection = CreateServicesMetricsCollectionJson(app);
                return JsonSerializer.SerializeAsync(context.Response.Body, servicesMetricsCollection, _options);
            }

            context.Response.ContentType = MediaTypeNames.Text.Plain;
            var response = CreateServicesMetricsCollectionText(app);
            return context.Response.WriteAsync(response);
        }

        private Task Metrics(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

            var name = (string?)context.Request.RouteValues["name"];

            if (string.IsNullOrEmpty(name) || !app.Services.TryGetValue(name, out var service))
            {
                context.Response.StatusCode = 404;
                return JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    message = $"Unknown service {name}"
                },
                _options);
            }

            var acceptHeader = context.Request.Headers[HeaderNames.Accept];

            if (acceptHeader == MediaTypeNames.Application.Json)
            {
                context.Response.ContentType = MediaTypeNames.Application.Json;
                var metricsCollectionJson = CreateMetricsCollectionJson(service);
                return JsonSerializer.SerializeAsync(context.Response.Body, metricsCollectionJson, _options);
            }

            context.Response.ContentType = MediaTypeNames.Text.Plain;
            var response = CreateMetricsCollectionText(service);
            return context.Response.WriteAsync(response);
        }

        private static string CreateServicesMetricsCollectionText(Application app)
        {
            var sb = new StringBuilder();
            foreach (var (serviceName, service) in app.Services.OrderBy(s => s.Key))
            {
                sb.AppendLine($"# {serviceName}");
                foreach (var (instance, replica) in service.Replicas)
                {
                    foreach (var (key, value) in replica.Metrics)
                    {
                        sb.Append(key);
                        sb.Append("{");
                        sb.Append($"service=\"{serviceName}\",");
                        sb.Append($"instance=\"{instance}\"");
                        sb.Append("}");
                        sb.Append(" ");
                        sb.Append(value);
                        sb.AppendLine();
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static List<V1ServiceMetrics> CreateServicesMetricsCollectionJson(Application app)
        {
            var servicesMetrics = new List<V1ServiceMetrics>(app.Services.Count);
            foreach (var (serviceName, service) in app.Services.OrderBy(s => s.Key))
            {
                var serviceMetrics = new V1ServiceMetrics
                {
                    Service = serviceName,
                    Metrics = CreateMetricsCollectionJson(service)
                };
                servicesMetrics.Add(serviceMetrics);
            }

            return servicesMetrics;
        }

        private static string CreateMetricsCollectionText(Service service)
        {
            var sb = new StringBuilder();

            foreach (var (instance, replica) in service.Replicas)
            {
                foreach (var (key, value) in replica.Metrics)
                {
                    sb.Append(key);
                    sb.Append("{");
                    sb.Append($"instance=\"{instance}\"");
                    sb.Append("}");
                    sb.Append(" ");
                    sb.Append(value);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static List<V1Metric> CreateMetricsCollectionJson(Service service)
        {
            var metrics = new List<V1Metric>();
            foreach (var (instance, replica) in service.Replicas)
            {
                foreach (var (key, value) in replica.Metrics)
                {
                    var metric = new V1Metric
                    {
                        Name = key,
                        Value = value,
                        Metadata = new V1MetricMetadata { Instance = instance }
                    };
                    metrics.Add(metric);
                }
            }

            return metrics;
        }
    }
}
