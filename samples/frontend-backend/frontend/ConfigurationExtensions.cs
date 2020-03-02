using System;

namespace Microsoft.Extensions.Configuration
{
    public static class ConfigurationExtensions
    {
        public static Uri? GetServiceUri(this IConfiguration configuration, string name)
        {
            var host = configuration[$"service:{name}:host"];
            var port = configuration[$"service:{name}:port"];
            var protocol = configuration[$"service:{name}:protocol"] ?? "http";

            if (string.IsNullOrEmpty(host) || port == null)
            {
                return null;
            }

            return new Uri(protocol + "://" + host + ":" + port + "/");
        }
    }
}