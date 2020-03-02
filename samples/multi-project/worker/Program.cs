using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.KeyPerFile;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(config =>
                {
                    if (Directory.Exists("/var/tye/bindings/"))
                    {
                        foreach (var directory in Directory.GetDirectories("/var/tye/bindings/"))
                        {
                            Console.WriteLine($"Adding config in '{directory}'.");
                            config.AddKeyPerFile(directory, optional: true);
                        }
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<QueueWorker>();
                });
    }
}
