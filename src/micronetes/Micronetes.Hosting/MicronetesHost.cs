using System;
using System.Linq;
using System.Threading.Tasks;
using Micronetes.Hosting.Diagnostics;
using Micronetes.Hosting.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Filters;

namespace Micronetes.Hosting
{
    public class MicronetesHost
    {
        public static async Task RunAsync(Application application, string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Logging for this application
            builder.Host.UseSerilog((context, configuration) => configuration
                .MinimumLevel.Verbose()
                .Filter.ByExcluding(Matching.FromSource("Microsoft"))
                .Enrich
                .FromLogContext()
                .WriteTo
                .Console()
            );

            builder.Services.AddRazorPages(o => o.RootDirectory = "/Dashboard/Pages");

            builder.Services.AddServerSideBlazor();

            builder.Services.AddOptions<StaticFileOptions>()
                .PostConfigure(o =>
                {
                    var fileProvider = new ManifestEmbeddedFileProvider(typeof(MicronetesHost).Assembly, "wwwroot");

                    // Make sure we don't remove the existing file providers (blazor needs this)
                    o.FileProvider = new CompositeFileProvider(o.FileProvider, fileProvider);
                });

            builder.Services.AddSingleton(application);

            using var app = builder.Build();

            var port = app.Configuration["port"] ?? "0";

            app.Listen($"http://127.0.0.1:{port}");

            app.UseDeveloperExceptionPage();

            app.UseStaticFiles();

            app.UseRouting();

            var api = new MicronetesApi();

            api.MapRoutes(app);

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            var logger = app.Logger;

            logger.LogInformation("Executing application from  {Source}", application.Source);

            var lifetime = app.ApplicationLifetime;
            var configuration = app.Configuration;

            var diagnosticOptions = DiagnosticOptions.FromConfiguration(configuration);
            var diagnosticsCollector = new DiagnosticsCollector(logger, diagnosticOptions);

            // Print out what providers were selected and their values
            diagnosticOptions.DumpDiagnostics(logger);

            var processor = new AggregateApplicationProcessor(new IApplicationProcessor[] {
                new EventPipeDiagnosticsRunner(logger, diagnosticsCollector),
                new ProxyService(logger),
                new DockerRunner(logger),
                new ProcessRunner(logger, ProcessRunnerOptions.FromArgs(args)),
            });

            await app.StartAsync();

            logger.LogInformation("Dashboard running on {Address}", app.Addresses.First());

            try
            {
                await processor.StartAsync(application);
            }
            catch (Exception ex)
            {
                logger.LogError(0, ex, "Failed to launch application");
            }

            var waitForStop = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            lifetime.ApplicationStopping.Register(obj => waitForStop.TrySetResult(null), null);

            await waitForStop.Task;

            logger.LogInformation("Shutting down...");

            try
            {
                await processor.StopAsync(application);
            }
            finally
            {
                // Stop the host after everything else has been shutdown
                await app.StopAsync();
            }
        }
    }
}
