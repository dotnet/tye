using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace volume_test
{
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
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    if (!File.Exists("/data/file.txt"))
                    {
                        context.Response.StatusCode = 404;
                        return;
                    }
                    var data = await File.ReadAllTextAsync("/data/file.txt");
                    await context.Response.WriteAsync(data);
                });

                endpoints.MapPost("/", async context =>
                {
                    await File.WriteAllTextAsync("/data/file.txt", await new StreamReader(context.Request.Body).ReadToEndAsync());

                    context.Response.StatusCode = 202;
                });
            });
        }
    }
}
