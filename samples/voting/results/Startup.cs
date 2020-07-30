using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;

namespace Results
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthorization(o => o.FallbackPolicy = o.DefaultPolicy);
            services.AddRazorPages(); 
            services.AddServerSideBlazor();


            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
            
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "Cookies";
                    options.DefaultChallengeScheme = "oidc";
                })
                .AddCookie("Cookies")
                .AddOpenIdConnect("oidc", options =>
                {
                    options.Authority = $"{Configuration["public-ip"]}/identityserver/";
                    options.ClientId = "interactive";
                    options.ClientSecret = "49C1A7E1-0C79-4A89-A3D6-A37998FB86B0";
                    options.ResponseType = "code";
                    options.SaveTokens = true;
                    options.RequireHttpsMetadata = false;
                    options.ReturnUrlParameter = $"{Configuration["public-ip"]}/results";
                    options.Events.OnRedirectToIdentityProvider = n =>
                    {
                        n.ProtocolMessage.RedirectUri = $"{Configuration["public-ip"]}/results/signin-oidc";
                        return Task.CompletedTask;
                    };
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UsePathBase("/results");

            var khOptions = new ForwardedHeadersOptions()
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            khOptions.KnownProxies.Clear();
            khOptions.KnownNetworks.Clear();

            app.UseForwardedHeaders(khOptions);

            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub().RequireAuthorization();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
