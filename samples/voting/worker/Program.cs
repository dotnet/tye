using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using System.IO;

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
                .ConfigureAppConfiguration(builder => AddTyeBindingSecrets(builder))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        
        private static void AddTyeBindingSecrets(IConfigurationBuilder config)
        {
            if (Directory.Exists("/var/tye/bindings/"))
            {
                foreach (var directory in Directory.GetDirectories("/var/tye/bindings/"))
                {
                    Console.WriteLine($"Adding config in '{directory}'.");
                    config.AddKeyPerFile(directory, optional: true);
                }
            }
        }
    }
}
