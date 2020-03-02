using System;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(config =>
                {
                    foreach (var directory in Directory.GetDirectories("/var/tye/bindings/"))
                    {
                        Console.WriteLine($"Adding config in '{directory}'.");
                        config.AddKeyPerFile(directory, optional: true);
                    }
                })
                .ConfigureWebHostDefaults(web =>
                {
                    web.UseStartup<Startup>()
                       .ConfigureKestrel(options =>
                       {
                           options.ConfigureEndpointDefaults(o => o.Protocols = HttpProtocols.Http2);
                       });
                });
    }
}
