// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Tye.Hosting.Diagnostics;
using Tye.Hosting.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog.Core;
using Serilog.Events;

namespace Tye.Hosting
{
    public class TyeHost
    {
        private Microsoft.Extensions.Logging.ILogger? _logger;
        private IHostApplicationLifetime? _lifetime;
        private AggregateApplicationProcessor? _processor;

        private readonly Application _application;
        private readonly string[] _args;

        public TyeHost(Application application, string[] args)
        {
            _application = application;
            _args = args;
        }

        public WebApplication? DashboardWebApplication { get; set; }

        // An additional sink that output will be piped to. Useful for testing.
        public ILogEventSink? Sink { get; set; }

        public async Task RunAsync()
        {
            await StartAsync();

            var waitForStop = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _lifetime?.ApplicationStopping.Register(obj => waitForStop.TrySetResult(null), null);
            await waitForStop.Task;

            await StopAsync();
        }

        public async Task<WebApplication> StartAsync()
        {
            var app = BuildWebApplication(_application, _args, Sink);
            DashboardWebApplication = app;

            ConfigureApplication(app);

            _logger = app.Logger;
            _lifetime = app.ApplicationLifetime;

            _logger.LogInformation("Executing application from  {Source}", _application.Source);

            var configuration = app.Configuration;

            _processor = CreateApplicationProcessor(_args, _logger, configuration);

            await app.StartAsync();

            _logger.LogInformation("Dashboard running on {Address}", app.Addresses.First());

            try
            {
                await _processor.StartAsync(_application);
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Failed to launch application");
            }

            return app;
        }

        public async Task StopAsync()
        {
            try
            {
                if (_processor != null)
                {
                    await _processor.StopAsync(_application);
                }
            }
            finally
            {
                if (DashboardWebApplication != null)
                {
                    // Stop the host after everything else has been shutdown
                    await DashboardWebApplication.StopAsync();
                    DashboardWebApplication.Dispose();
                }
            }
        }

        private static WebApplication BuildWebApplication(
            Application application,
            string[] args,
            ILogEventSink? sink)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Logging for this application
            builder.Host.UseSerilog((context, configuration) =>
            {
                configuration
                    .MinimumLevel.Verbose()
                    .Filter.ByExcluding(Matching.FromSource("Microsoft"))
                    .Enrich
                    .FromLogContext()
                    .WriteTo
                    .Console();

                if (sink is object)
                {
                    configuration.WriteTo.Sink(sink, LogEventLevel.Verbose);
                }
            });

            builder.Services.AddRazorPages(o => o.RootDirectory = "/Dashboard/Pages");

            builder.Services.AddServerSideBlazor();

            builder.Services.AddOptions<StaticFileOptions>()
                .PostConfigure(o =>
                {
                    var fileProvider = new ManifestEmbeddedFileProvider(typeof(TyeHost).Assembly, "wwwroot");

                    // Make sure we don't remove the existing file providers (blazor needs this)
                    o.FileProvider = new CompositeFileProvider(o.FileProvider, fileProvider);
                });

            builder.Services.AddSingleton(application);
            var app = builder.Build();
            return app;
        }

        private static void ConfigureApplication(WebApplication app)
        {
            var port = app.Configuration["port"] ?? "0";

            app.Listen($"http://127.0.0.1:{port}");

            app.UseDeveloperExceptionPage();

            app.UseStaticFiles();

            app.UseRouting();

            var api = new TyeDashboardApi();

            api.MapRoutes(app);

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");
        }

        private static AggregateApplicationProcessor CreateApplicationProcessor(string[] args, Microsoft.Extensions.Logging.ILogger logger, IConfiguration configuration)
        {
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
            return processor;
        }
    }
}
