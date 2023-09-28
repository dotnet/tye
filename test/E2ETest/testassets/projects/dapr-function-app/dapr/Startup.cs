using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Dapr.Client;

namespace dapr
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
                    var functionAppId = "dapr-function-app";
                    var methodName = "api/HttpTrigger1";
                    
                    
                    var daprClient = new DaprClientBuilder().Build();
                    var daprRequest = daprClient.CreateInvokeMethodRequest(HttpMethod.Post, functionAppId, methodName);

                    var response = await daprClient.InvokeMethodWithResponseAsync(daprRequest);
                    
                    await context.Response.WriteAsync(await response.Content.ReadAsStringAsync());
                });
            });
        }
    }
}
