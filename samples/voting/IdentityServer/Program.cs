// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;

namespace IdentityServer
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // Log.Logger = new LoggerConfiguration()
            //     .MinimumLevel.Debug()
            //     .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            //     .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            //     .MinimumLevel.Override("System", LogEventLevel.Warning)
            //     .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
            //     .Enrich.FromLogContext()
            //     // uncomment to write to Azure diagnostics stream
            //     //.WriteTo.File(
            //     //    @"D:\home\LogFiles\Application\identityserver.txt",
            //     //    fileSizeLimitBytes: 1_000_000,
            //     //    rollOnFileSizeLimit: true,
            //     //    shared: true,
            //     //    flushToDiskInterval: TimeSpan.FromSeconds(1))
            //     .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}", theme: AnsiConsoleTheme.Code)
            //     .CreateLogger();

            // Log.Information("Starting host...");
            CreateHostBuilder(args).Build().Run();
            return 0;
        
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}