using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Vote
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception x)
            {
                Console.WriteLine(x.Message);
            }
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
