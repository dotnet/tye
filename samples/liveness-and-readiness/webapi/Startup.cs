// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace webapi
{
    public class Startup
    {
        private string _id;

        private ConcurrentDictionary<string, bool> _statusDictionary;

        public Startup()
        {
            _id = Guid.NewGuid().ToString();
            _statusDictionary = new ConcurrentDictionary<string, bool>()
            {
                ["someLivenessCheck"] = true,
                ["someReadinessCheck"] = true
            };
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddHealthChecks()
                // this registers a "liveness" check. A service that fails a liveness check is considered to be unrecoverable and has to be restarted by the orchestrator (Tye/Kubernetes).
                // for example: you may consider failing this check if your service has encountered a fatal exception, or if you've detected a memory leak or a substantially long average response time
                .AddCheck("someLivenessCheck", new MyGenericCheck(_statusDictionary, "someLivenessCheck"), failureStatus: HealthStatus.Unhealthy, tags: new[] { "liveness" })
                // this registers a "readiness" check. A service that fails a readiness check is considered to be unable to serve traffic temporarily. The orchestrator doesn't restart a service that fails this check, but stops sending traffic to it until it responds to this check positively again.
                // for example: you may consider failing this check if your service is currently unable to connect to some external service such as your database, cache service, etc...
                .AddCheck("someReadinessCheck", new MyGenericCheck(_statusDictionary, "someReadinessCheck"), failureStatus: HealthStatus.Unhealthy, tags: new[] { "readiness" });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync($@"
                    Hello World! Process Id: {_id}<br />
                    This sample service exposes an HTTP GET endpoint <b>/set</b> that allows you to change the results of the liveness/readiness probes. <br /><br />
                    Examples: <br /><br />
                    <b>GET /set?someReadinessCheck=false&timeout=10</b> will cause the readiness probe to fail for 10 seconds.<br />
                    <b>GET /set?someLivenessCheck=false</b> will cause the liveness probe to fail, resulting in a restart of that replica.
                    ");
                });

                // this endpoint returns HTTP 200 if all "liveness" checks have passed, otherwise, it returns HTTP 500
                endpoints.MapHealthChecks("/lively", new HealthCheckOptions()
                {
                    Predicate = reg => reg.Tags.Contains("liveness")
                });

                // this endpoint returns HTTP 200 if all "readiness" checks have passed, otherwise, it returns HTTP 500
                endpoints.MapHealthChecks("/ready", new HealthCheckOptions()
                {
                    Predicate = reg => reg.Tags.Contains("readiness")
                });

                // Should be technically POST/PUT, but it's just for tests...
                endpoints.MapGet("/set", async context =>
                {
                    var query = context.Request.Query.ToDictionary(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value.Value.First());
                    var statusesFromQuery = query.Where(kv => kv.Key != "timeout").ToDictionary(kv => kv.Key, kv => kv.Value.Trim().ToLower() == "true");

                    var statusesSnapshot = _statusDictionary.Where(kv => statusesFromQuery.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);

                    foreach (var status in statusesFromQuery)
                    {
                        _statusDictionary[status.Key] = status.Value;
                    }

                    if (query.ContainsKey("timeout") && int.TryParse(query["timeout"], out var timeout))
                    {
                        var _ = Task.Delay(TimeSpan.FromSeconds(timeout))
                            .ContinueWith(_ =>
                            {
                                foreach (var previousStatus in statusesSnapshot)
                                {
                                    _statusDictionary[previousStatus.Key] = previousStatus.Value;
                                }
                            });
                    }

                    await context.Response.WriteAsync("Done");
                });
            });
        }
    }
}
