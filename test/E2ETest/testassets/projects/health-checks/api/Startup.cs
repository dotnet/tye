// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace api
{
    public class Startup
    {
        private static string _randomId = Guid.NewGuid().ToString();

        private static bool _healthy = false;
        private static bool _ready = false;

        private static int _healthyDelay = 0;
        private static int _readyDelay = 0;

        private static Dictionary<string, string> _livenessHeaders;
        private static Dictionary<string, string> _readinessHeaders;

        private static int[] _ports;
        
        private static object _locker = new object();
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            var healthyEnv = Environment.GetEnvironmentVariable("healthy");
            var readyEnv = Environment.GetEnvironmentVariable("ready");

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORT")))
            {
                var portParts = Environment.GetEnvironmentVariable("PORT").Split(';');
                _ports = portParts.Select(p => int.Parse(p)).ToArray();
            }

            if (healthyEnv != null)
            {
                _healthy = bool.Parse(healthyEnv);
            }

            if (readyEnv != null)
            {
                _ready = bool.Parse(readyEnv);
            }
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async ctx =>
                {
                    await ctx.Response.WriteAsync("Hello");
                });

                endpoints.MapGet("/ports", async ctx =>
                {
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(_ports));
                });

                endpoints.MapGet("/id", async ctx =>
                {
                    await ctx.Response.WriteAsync(_randomId);
                });

                endpoints.MapGet("/healthy", async ctx =>
                {
                    if (_healthyDelay != 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_healthyDelay));
                    }
                    
                    _livenessHeaders = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
                    
                    ctx.Response.StatusCode = _healthy ? 200 : 500;
                    await ctx.Response.WriteAsync(ctx.Response.StatusCode.ToString());
                });

                endpoints.MapGet("/ready", async ctx =>
                {
                    if (_readyDelay != 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_readyDelay));
                    }
                    
                    _readinessHeaders = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
                    
                    ctx.Response.StatusCode = _ready ? 200 : 500;
                    await ctx.Response.WriteAsync(ctx.Response.StatusCode.ToString());
                });

                // Should be technically POST/PUT, but it's just for tests...
                endpoints.MapGet("/set", async ctx =>
                {
                    var query = ctx.Request.Query.ToDictionary(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value.Value.First());
                    if (query.ContainsKey("healthy"))
                    {
                        _healthy = bool.Parse(query["healthy"]);
                    }

                    if (query.ContainsKey("ready"))
                    {
                        _ready = bool.Parse(query["ready"]);
                    }

                    if (query.ContainsKey("healthyDelay"))
                    {
                        _healthyDelay = int.Parse(query["healthyDelay"]);
                    }

                    if (query.ContainsKey("readyDelay"))
                    {
                        _readyDelay = int.Parse(query["readyDelay"]);
                    }

                    await ctx.Response.WriteAsync(_randomId);
                });
                
                endpoints.MapGet("/livenessHeaders", async ctx =>
                {
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(_livenessHeaders));
                });

                endpoints.MapGet("/readinessHeaders", async ctx =>
                {
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(_readinessHeaders));
                });
            });
        }
    }
}
