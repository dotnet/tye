using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Worker
{
    public static class Extensions
    {
        public static string GetSqlConnectionString(this IConfiguration configuration)
        {
            return configuration["connectionstring:postgres"] ?? $"Server={configuration["service:postgres:host"]};Port={configuration["service:postgres:port"]};User Id=postgres;Password=pass@word1;";
        }
    }
}
