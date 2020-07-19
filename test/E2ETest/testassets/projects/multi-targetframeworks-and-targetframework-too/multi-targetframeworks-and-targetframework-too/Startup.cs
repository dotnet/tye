// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using System.Runtime.Versioning;

namespace MultiTargetFrameworks
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
            app.Use(async (httpContext, next) =>
            {
#if NETCOREAPP3_1
                await httpContext.Response.WriteAsync(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
#else
                var framework = Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
                await httpContext.Response.WriteAsync(framework);
#endif
            });
        }
    }
}
