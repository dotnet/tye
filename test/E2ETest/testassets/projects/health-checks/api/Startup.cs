using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private static bool _healthy = true;
        private static bool _ready = false;
        
        private static object _locker = new object();
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
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

                endpoints.MapGet("/id", async ctx =>
                {
                    await ctx.Response.WriteAsync(_randomId);
                });

                endpoints.MapGet("/healthy", async ctx =>
                {
                    ctx.Response.StatusCode = _healthy ? 200 : 500;
                    await ctx.Response.WriteAsync(ctx.Response.StatusCode.ToString());
                });

                endpoints.MapGet("/ready", async ctx =>
                {
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

                    await ctx.Response.WriteAsync(_randomId);
                });
            });
        }
    }
}
