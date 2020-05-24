// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace webapi
{
    class SetDTO
    {
        public bool? Healthy { get; set; }
        public bool? Ready { get; set; }
        public int? Timeout{ get; set; }
    }

    public class Startup
    {
        private string _id;
        private bool _healthy;
        private bool _ready;

        public Startup()
        {
            _id = Guid.NewGuid().ToString();
            _healthy = true;
            _ready = true;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
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
                    await context.Response.WriteAsync($"Hello World! Process Id: {_id}");
                });

                endpoints.MapGet("/healthy", async context =>
                {
                    context.Response.StatusCode = _healthy ? 200 : 500;
                    await context.Response.WriteAsync($"Status Code: {context.Response.StatusCode}");
                });

                endpoints.MapGet("/ready", async context =>
                {
                    context.Response.StatusCode = _ready ? 200 : 500;
                    await context.Response.WriteAsync($"Status Code: {context.Response.StatusCode}");
                });

                endpoints.MapPost("/set", async context =>
                {
                    var data = await JsonSerializer.DeserializeAsync<SetDTO>(context.Request.Body);
                    
                    var originalHealthy = _healthy;
                    var originalReady = _ready;

                    if (data.Healthy.HasValue)
                    {
                        _healthy = data.Healthy.Value;
                    }

                    if (data.Ready.HasValue)
                    {
                        _ready = data.Ready.Value;
                    }

                    if (data.Timeout.HasValue)
                    {
                        var _ = Task.Delay(TimeSpan.FromSeconds(data.Timeout.Value))
                            .ContinueWith(_ =>
                            {
                                _healthy = originalHealthy;
                                _ready = originalReady;
                            });
                    }

                    await context.Response.WriteAsync("Done");
                });
            });
        }
    }
}
