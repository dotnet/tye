using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Proxy;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Tye.HttpProxy
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<MatcherPolicy, IngressHostMatcherPolicy>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, ILogger<Startup> logger, IWebHostEnvironment env, IConfiguration configuration)
        {
            var invoker = new HttpMessageInvoker(new ConnectionRetryHandler(new RoundRobinLoadBalancer(logger, new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseProxy = false
            })));

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                foreach (var rule in configuration.GetSection("Rules").GetChildren())
                {
                    var host = rule["Host"];
                    var path = rule["Path"];
                    var preservePath = rule.GetSection("PreservePath").Get<bool>();
                    var service = rule["Service"];
                    var port = rule["Port"];
                    var protocol = rule["Protocol"];

                    var url = $"{protocol}://{service}:{port}";

                    RequestDelegate del = context =>
                    {
                        var uri = new UriBuilder(url)
                        {
                            Path = preservePath ? context.Request.Path.ToString() : (string)context.Request.RouteValues["path"] ?? "/"
                        };

                        return context.ProxyRequest(invoker, uri.Uri);
                    };

                    IEndpointConventionBuilder conventions = null!;

                    if (path != null)
                    {
                        conventions = endpoints.Map(path.TrimEnd('/') + "/{**path}", del);
                    }
                    else
                    {
                        conventions = endpoints.MapFallback(del);
                    }

                    if (host != null)
                    {
                        conventions.WithMetadata(new IngressHostMetadata(host));
                    }
                }
            });
        }
    }
}
