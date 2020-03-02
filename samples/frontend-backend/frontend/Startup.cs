using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frontend
{
    public class Startup
    {
        private readonly JsonSerializerOptions options = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHealthChecks();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                var httpClient = new HttpClient()
                {
                    BaseAddress = Configuration.GetServiceUri("backend"),
                };

                endpoints.MapGet("/", async context =>
                {
                    var bytes = await httpClient.GetByteArrayAsync("/");
                    var backendInfo = JsonSerializer.Deserialize<BackendInfo>(bytes, options);

                    await context.Response.WriteAsync($"Frontend Listening IP: {context.Connection.LocalIpAddress}{Environment.NewLine}");
                    await context.Response.WriteAsync($"Frontend Hostname: {Dns.GetHostName()}{Environment.NewLine}");
                    await context.Response.WriteAsync($"EnvVar Configuration value: {Configuration["App:Value"]}{Environment.NewLine}");

                    await context.Response.WriteAsync($"Backend Listening IP: {backendInfo.IP}{Environment.NewLine}");
                    await context.Response.WriteAsync($"Backend Hostname: {backendInfo.Hostname}{Environment.NewLine}");
                });
                
                endpoints.MapHealthChecks("/healthz");
            });
        }

        class BackendInfo
        {
            public string IP { get; set; } = default!;

            public string Hostname { get; set; } = default!;
        }
    }
}
