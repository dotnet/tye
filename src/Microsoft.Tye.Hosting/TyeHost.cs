// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
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
        private ICollection<string>? _addresses;
        private int _computedPort;
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

        public IHost? DashboardWebApplication { get; set; }

        public ICollection<string>? Addresses => _addresses;

        public Extensions.Logging.ILogger? Logger => _logger;

        // An additional sink that output will be piped to. Useful for testing.
        public ILogEventSink? Sink { get; set; }

        public async Task RunAsync()
        {
            try
            {
                await StartAsync();

                var waitForStop = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _lifetime?.ApplicationStopping.Register(obj =>
                {
                    _logger?.LogInformation("Tye Host is stopping...");
                    waitForStop.TrySetResult(null);
                }, null);
                await waitForStop.Task;
            }
            finally
            {
                await StopAsync();
            }
        }

        public async Task<IHost> StartAsync()
        {
            var app = BuildWebApplication(_application, _options, Sink);
            DashboardWebApplication = app;

            _logger = DashboardWebApplication.Services.GetRequiredService<ILogger<TyeHost>>();
            _lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

            if (_computedPort != _options.Port && _computedPort != DefaultPort)
            {
                _logger.LogInformation("Default dashboard port {DefaultPort} has been reserved by the application or is in use, choosing random port.", DefaultPort);
            }

            _logger.LogInformation("Executing application from {Source}", _application.Source);

            _replicaRegistry = new ReplicaRegistry(_application.ContextDirectory, _logger);

            _processor = CreateApplicationProcessor(_replicaRegistry, _options, _logger);

            await app.StartAsync();

            _addresses = DashboardWebApplication.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;

            var dashboardAddress = _addresses?.FirstOrDefault();

            if (dashboardAddress != null)
            {
                _logger.LogInformation("Dashboard running on {Address}", dashboardAddress);
            }
            else
            {
                _logger.LogWarning("Dashboard is not running");
            }

            try
            {
                await _processor.StartAsync(_application);
                _logger.LogInformation($"Application {_application.Name} started successfully with Pid: {Process.GetCurrentProcess().Id}");
            }
            catch (TyeBuildException ex)
            {
                _logger.LogError(ex.Message);
                _lifetime.StopApplication();
            }

            if (dashboardAddress != null && _options.Dashboard)
            {
                OpenDashboard(dashboardAddress);
            }

            return app;
        }

        private IHost BuildWebApplication(Application application, HostOptions options, ILogEventSink? sink)
        {
            return Host.CreateDefaultBuilder()
                .UseSerilog((context, configuration) =>
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
                })
                .ConfigureWebHostDefaults(builder =>
                {
                    var port = ComputePort(options.Port ?? application.DashboardPort);
                    _computedPort = port;

                    builder.Configure(ConfigureApplication)
                            .UseUrls($"http://127.0.0.1:{port}");
                    builder.ConfigureAppConfiguration((b, c) =>
                    {
                        b.HostingEnvironment.ApplicationName = "Microsoft.Tye.Hosting";
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddRazorPages(o =>
                    {
                        o.RootDirectory = "/Dashboard/Pages";
                    });

                    services.AddServerSideBlazor();
                    services.AddOptions<StaticFileOptions>()
                            .PostConfigure(o =>
                            {
                                var fileProvider = new ManifestEmbeddedFileProvider(typeof(TyeHost).Assembly, "wwwroot");

                                // Make sure we don't remove the existing file providers (blazor needs this)
                                o.FileProvider = new CompositeFileProvider(o.FileProvider, fileProvider);
                            });
                    services.AddCors(
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
                    services.AddSingleton(application);
                })
                .Build();
        }

        private void ConfigureApplication(IApplicationBuilder app)
        {
            app.UseDeveloperExceptionPage();

            app.UseCors("default");

            app.UseStaticFiles();

            app.UseRouting();

            var api = new TyeDashboardApi();

            app.UseEndpoints(endpoints =>
            {
                api.MapRoutes(endpoints);
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }

        private int ComputePort(int? port)
        {
            // logic for computing the port:
            // - we allow the user to specify the port... if they don't
            // - we want to use a predictable port so that it's easy to remember how to
            //   get to the dashboard ... and
            // - we don't want to cause conflicts with any of the users known bindings
            //   or something else running.

            if (port.HasValue)
            {
                // Port was passed in at the command-line, use it!
                return port.Value;
            }

            if (IsPortInUseByBinding(_application, DefaultPort))
            {
                // Port has been reserved for the app.
                return AutodetectPort;
            }

            if (!IsPortAlreadyInUse(DefaultPort))
            {
                return DefaultPort;
            }

            // Port is in use by something already running.
            return AutodetectPort;
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
            foreach (var (_, service) in application.Services)
            {
                if (service.Description.Bindings.Any(binding => binding.Port == port))
                {
                    return true;
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

            if (options.LoggingProvider != null &&
                DiagnosticsProvider.TryParse(options.LoggingProvider, out var logging))
            {
                diagnosticsCollector.LoggingSink = new LoggingSink(logger, logging);
            }

            if (options.DistributedTraceProvider != null &&
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
                new FuncFinder(logger),
                new ReplicaMonitor(logger),
                new DockerRunner(logger, replicaRegistry, DockerRunnerOptions.FromHostOptions(options)),
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
                _logger?.LogError(ex, "Error while shutting down");
            }
            finally
            {
                if (DashboardWebApplication != null)
                {
                    // Stop the host after everything else has been shutdown
                    try
                    {
                        await DashboardWebApplication.StopAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore cancellation failures from stop async
                    }
                }
            }

            _processor = null;
        }

        private void OpenDashboard(string url)
        {
            try
            {
                // https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error launching dashboard.");
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
                    .Where(p => p != null)!);

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
