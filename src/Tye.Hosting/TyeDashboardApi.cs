using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tye.Hosting.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Tye.Hosting
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

            _options.Converters.Add(ReplicaStatus.JsonConverter);
        }

        public void MapRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/v1", ServiceIndex);
            endpoints.MapGet("/api/v1/services", Services);
            endpoints.MapGet("/api/v1/services/{name}", Service);
            endpoints.MapGet("/api/v1/logs/{name}", Logs);
            endpoints.MapGet("/api/v1/metrics", AllMetrics);
            endpoints.MapGet("/api/v1/metrics/{name}", Metics);
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

            await JsonSerializer.SerializeAsync(context.Response.Body, services, _options);
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

            await JsonSerializer.SerializeAsync(context.Response.Body, service, _options);
        }

        private async Task Logs(HttpContext context)
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

            await JsonSerializer.SerializeAsync(context.Response.Body, service.CachedLogs, _options);
        }

        private async Task AllMetrics(HttpContext context)
        {
            var app = context.RequestServices.GetRequiredService<Application>();

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

        private async Task Metics(HttpContext context)
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
