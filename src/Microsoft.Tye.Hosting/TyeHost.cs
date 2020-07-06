// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Diagnostics;
using Microsoft.Tye.Hosting.Model;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Filters;

namespace Microsoft.Tye.Hosting
{
    public class TyeHost : IAsyncDisposable
    {
        private const int DefaultPort = 8000;
        private const int AutodetectPort = 0;

        private Microsoft.Extensions.Logging.ILogger? _logger;
        private IHostApplicationLifetime? _lifetime;
        private AggregateApplicationProcessor? _processor;

        private readonly Application _application;
        private readonly HostOptions _options;
        private ReplicaRegistry? _replicaRegistry;

        public TyeHost(Application application, HostOptions options)
        {
            _application = application;
            _options = options;
        }

        public Application Application => _application;

        public WebApplication? DashboardWebApplication { get; set; }

        // An additional sink that output will be piped to. Useful for testing.
        public ILogEventSink? Sink { get; set; }

        public async Task RunAsync()
        {
            try
            {
                await StartAsync();

                var waitForStop = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _lifetime?.ApplicationStopping.Register(obj => waitForStop.TrySetResult(null), null);
                await waitForStop.Task;
            }
            finally
            {
                await StopAsync();
            }
        }

        public async Task<WebApplication> StartAsync()
        {
            // TODO: move to proper location.

            if (string.IsNullOrEmpty(Application.Network))
            {
                // rootless podman doesn't permit creation of networks.
                // Use the host network instead, and perform communication between applications using "localhost".

                bool isPodman = await DockerDetector.Instance.IsPodman.Value;
                Application.UseHostNetwork = isPodman;
            }
            else if (Application.Network == "host")
            {
                Application.UseHostNetwork = true;
            }

            var app = BuildWebApplication(_application, _options, Sink);
            DashboardWebApplication = app;

            _logger = app.Logger;
            _lifetime = app.ApplicationLifetime;

            _logger.LogInformation("Executing application from {Source}", _application.Source);

            ConfigureApplication(app);

            _replicaRegistry = new ReplicaRegistry(_application.ContextDirectory, _logger);

            _processor = CreateApplicationProcessor(_replicaRegistry, _options, _logger);

            await app.StartAsync();

            _logger.LogInformation("Dashboard running on {Address}", app.Addresses.First());

            await _processor.StartAsync(_application);

            if (_options.Dashboard)
            {
                OpenDashboard(app.Addresses.First());
            }

            return app;
        }

        private static WebApplication BuildWebApplication(Application application, HostOptions options, ILogEventSink? sink)
        {
            var args = new List<string>();
            if (options.Port.HasValue)
            {
                args.Add("--port");
                args.Add(options.Port.Value.ToString(CultureInfo.InvariantCulture));
            }

            var builder = WebApplication.CreateBuilder(args.ToArray());

            // Logging for this application
            builder.Host.UseSerilog((context, configuration) =>
            {
                var logLevel = options.LogVerbosity switch
                {
                    Verbosity.Quiet => LogEventLevel.Warning,
                    Verbosity.Info => LogEventLevel.Information,
                    Verbosity.Debug => LogEventLevel.Verbose,
                    _ => default
                };

                configuration
                    .MinimumLevel.Is(logLevel)
                    .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore"))
                    .Filter.ByExcluding(Matching.FromSource("Microsoft.Extensions"))
                    .Filter.ByExcluding(Matching.FromSource("Microsoft.Hosting"))
                    .Enrich
                    .FromLogContext()
                    .WriteTo
                    .Console();

                if (sink is object)
                {
                    configuration.WriteTo.Sink(sink, logLevel);
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

            builder.Services.AddCors(
                options =>
                {
                    options.AddPolicy(
                        "default",
                        policy =>
                        {
                            policy
                                .AllowAnyOrigin()
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                        });
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

            app.UseCors("default");

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
                app.Logger.LogInformation("Default dashboard port {DefaultPort} has been reserved by the application, choosing random port.", DefaultPort);
                return AutodetectPort;
            }
            else if (IsPortAlreadyInUse(DefaultPort))
            {
                // Port is in use by something already running.
                app.Logger.LogInformation("Default dashboard port {DefaultPort} is in use, choosing random port.", DefaultPort);
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

        private static bool IsPortInUseByBinding(Application application, int port)
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

        private static AggregateApplicationProcessor CreateApplicationProcessor(ReplicaRegistry replicaRegistry, HostOptions options, Microsoft.Extensions.Logging.ILogger logger)
        {
            var diagnosticsCollector = new DiagnosticsCollector(logger)
            {
                // Local run always uses metrics for the dashboard
                MetricSink = new MetricSink(logger),
            };

            if (options.LoggingProvider is string &&
                DiagnosticsProvider.TryParse(options.LoggingProvider, out var logging))
            {
                diagnosticsCollector.LoggingSink = new LoggingSink(logger, logging);
            }

            if (options.DistributedTraceProvider is string &&
                DiagnosticsProvider.TryParse(options.DistributedTraceProvider, out var tracing))
            {
                diagnosticsCollector.TracingSink = new TracingSink(logger, tracing);
            }

            // Print out what providers were selected and their values
            DumpDiagnostics(options, logger);

            var processors = new List<IApplicationProcessor>
            {
                new EventPipeDiagnosticsRunner(logger, diagnosticsCollector),
                new PortAssigner(logger),
                new ProxyService(logger),
                new HttpProxyService(logger),
                new DockerImagePuller(logger),
                new ReplicaMonitor(logger),
                new DockerRunner(logger, replicaRegistry),
                new ProcessRunner(logger, replicaRegistry, ProcessRunnerOptions.FromHostOptions(options))
            };

            // If the docker command is specified then transform the ProjectRunInfo into DockerRunInfo
            if (options.Docker)
            {
                processors.Insert(0, new TransformProjectsIntoContainers(logger));
            }

            return new AggregateApplicationProcessor(processors);
        }

        private async Task StopAsync()
        {
            try
            {
                if (_processor != null)
                {
                    await _processor.StopAsync(_application);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while shutting down");
            }
            finally
            {
                if (DashboardWebApplication != null)
                {
                    // Stop the host after everything else has been shutdown
                    await DashboardWebApplication.StopAsync();
                }
            }

            _processor = null;
        }

        private void OpenDashboard(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error launching dashboard.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();

            _replicaRegistry?.Dispose();
            DashboardWebApplication?.Dispose();
        }

        private static void DumpDiagnostics(HostOptions options, Microsoft.Extensions.Logging.ILogger logger)
        {
            var providerText = new List<string>();
            providerText.AddRange(
                new[] { options.DistributedTraceProvider, options.LoggingProvider, options.MetricsProvider }
                .Where(p => p is object)
                .Cast<string>());

            foreach (var text in providerText)
            {
                if (DiagnosticsProvider.TryParse(text, out var provider))
                {
                    if (DiagnosticsProvider.WellKnownProviders.TryGetValue(provider.Key, out var wellKnown))
                    {
                        logger.LogInformation(wellKnown.LogFormat, provider.Value);
                    }
                    else
                    {
                        logger.LogWarning("Unknown diagnostics provider {Key}:{Value}", provider.Key, provider.Value);
                    }
                }
                else
                {
                    logger.LogError("Could not parse provider argument: {Arg}", text);
                }
            }
        }
    }
}
