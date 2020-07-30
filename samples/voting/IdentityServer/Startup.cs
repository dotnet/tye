// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4;
using IdentityServerHost.Quickstart.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IdentityServer4.Models;
using System.Collections.Generic;
using System;
using IdentityServer4.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using Microsoft.AspNetCore.HttpOverrides;

namespace IdentityServer
{
    public class Startup
    {
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            Environment = environment;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            var publicIp = Configuration["public-ip"];

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
                // see https://identityserver4.readthedocs.io/en/latest/topics/resources.html
                options.EmitStaticAudienceClaim = true;
            })
                .AddTestUsers(TestUsers.Users);

            // in-memory, code config
            builder.AddInMemoryIdentityResources(Config.IdentityResources);
            
            // builder.AddInMemoryApiScopes(Config.ApiScopes);
            builder.AddInMemoryClients(new Client[]
            {
                // m2m client credentials flow client
                // interactive client using code flow + pkce
                new Client
                {
                    ClientId = "interactive",
                    ClientSecrets = { new Secret("49C1A7E1-0C79-4A89-A3D6-A37998FB86B0".Sha256()) },
                    
                    AllowedGrantTypes = GrantTypes.Code,

                    // These currently break when ingress is in place for redirect.
                    RedirectUris = { $"{publicIp}/results/signin-oidc"}, //  $"{Configuration.GetServiceUri("results:http")}results/signin-oidc", $"{Configuration.GetServiceUri("results:http")}signin-oidc"
                    FrontChannelLogoutUri = $"{publicIp}/results/signout-oidc",
                    PostLogoutRedirectUris = { $"{publicIp}/results/signout-callback-oidc" }, // , $"{Configuration.GetServiceUri("results:http")}results/signout-callback-oidc", $"{Configuration.GetServiceUri("results:http")}signout-callback-oidc"

                    AllowOfflineAccess = true,
                    AllowedScopes = new List<string>{IdentityServerConstants.StandardScopes.OpenId,IdentityServerConstants.StandardScopes.Profile, "scope"}
                },
            });

            // not recommended for production - you need to store your key material somewhere secure
            builder.AddDeveloperSigningCredential();
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            // app.UseForwardedHeaders();
            app.UsePathBase("/identityserver");

            app.UseStaticFiles();

            var khOptions = new ForwardedHeadersOptions()
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            khOptions.KnownProxies.Clear();
            khOptions.KnownNetworks.Clear();

            app.UseForwardedHeaders(khOptions);

            app.UseRouting();
            app.UseIdentityServer();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
