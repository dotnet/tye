using System;
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
    }

    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var id = Guid.NewGuid().ToString();
            var healthy = true;
            var ready = true;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync($"Hello World! Process Id: {id}");
                });

                endpoints.MapGet("/healthy", async context =>
                {
                    context.Response.StatusCode = healthy ? 200 : 500;
                    await context.Response.WriteAsync($"Status Code: {context.Response.StatusCode}");
                });

                endpoints.MapGet("/ready", async context =>
                {
                    context.Response.StatusCode = ready ? 200 : 500;
                    await context.Response.WriteAsync($"Status Code: {context.Response.StatusCode}");
                });

                endpoints.MapPost("/set", async context =>
                {
                    var data = await JsonSerializer.DeserializeAsync<SetDTO>(context.Request.Body);
                    
                    if (data.Healthy.HasValue)
                    {
                        healthy = data.Healthy.Value;
                    }

                    if (data.Ready.HasValue)
                    {
                        ready = data.Ready.Value;
                    }

                    await context.Response.WriteAsync("Done");
                });
            });
        }
    }
}
