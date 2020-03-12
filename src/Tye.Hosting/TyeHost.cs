﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Filters;
using Tye.Hosting.Diagnostics;
using Tye.Hosting.Model;

namespace Tye.Hosting
{
    public class TyeHost : IDisposable
    {
        private const int DefaultPort = 8000;
        private const int AutodetectPort = 0;

        private Microsoft.Extensions.Logging.ILogger? _logger;
        private IHostApplicationLifetime? _lifetime;
        private AggregateApplicationProcessor? _processor;

        private readonly Tye.Hosting.Model.Application _application;
        private readonly string[] _args;

        public TyeHost(Tye.Hosting.Model.Application application, string[] args)
        {
            _application = application;
            _args = args;
        }

        public Tye.Hosting.Model.Application Application => _application;

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

            _logger = app.Logger;
            _lifetime = app.ApplicationLifetime;

            _logger.LogInformation("Executing application from {Source}", _application.Source);

            ConfigureApplication(app);

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
                }
            }
        }

        private static WebApplication BuildWebApplication(
            Tye.Hosting.Model.Application application,
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

        private void ConfigureApplication(WebApplication app)
        {
            var port = ComputePort(app);

            app.Listen($"http://127.0.0.1:{port}");

            app.UseDeveloperExceptionPage();

            app.UseStaticFiles();

            app.UseRouting();

            var api = new TyeDashboardApi();

            api.MapRoutes(app);

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");
        }

        private int ComputePort(WebApplication app)
        {
            // logic for computing the port:
            // - we allow the user to specify the port... if they don't
            // - we want to use a predictable port so that it's easy to remember how to
            //   get to the dashboard ... and
            // - we don't want to cause conflicts with any of the users known bindings
            //   or something else running.

            var port = app.Configuration["port"];
            if (!string.IsNullOrEmpty(port))
            {
                // Port was passed in at the command-line, use it!
                return int.Parse(port, NumberStyles.Number, CultureInfo.InvariantCulture);
            }
            else if (IsPortInUseByBinding(_application, DefaultPort))
            {
                // Port has been reserved for the app.
                app.Logger.LogInformation($"Default dashboard port {DefaultPort} has been reserved by the application, choosing random port.");
                return AutodetectPort;
            }
            else if (IsPortAlreadyInUse(DefaultPort))
            {
                // Port is in use by something already running.
                app.Logger.LogInformation($"Default dashboard port {DefaultPort} is in use, choosing random port.");
                return AutodetectPort;
            }
            else
            {
                return DefaultPort;
            }
        }

        private static bool IsPortAlreadyInUse(int port)
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            try
            {
                using var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(endpoint);
                return false;
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                return true;
            }
        }

        private static bool IsPortInUseByBinding(Model.Application application, int port)
        {
            foreach (var service in application.Services)
            {
                foreach (var binding in service.Value.Description.Bindings)
                {
                    if (binding.Port == port)
                    {
                        return true;
                    }
                }
            }

            return false;
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

        public void Dispose()
        {
            DashboardWebApplication?.Dispose();
        }
    }
}
