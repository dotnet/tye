using System;
using Microsoft.Extensions.Configuration;

namespace Results
{
    public static class ConfigurationExtensions
    {
        public static string GetUri(this IConfiguration configuration, string name)
        {
            return $"http://{configuration[$"service:{name}:host"]}:{configuration[$"service:{name}:port"]}";
        }
    }
}
