using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Hosting.Model
{
    public class IngressRule
    {
        public IngressRule(string? host, string? path, string service)
        {
            Host = host;
            Path = path;
            Service = service;
        }

        public string? Host { get; }
        public string? Path { get; }
        public string Service { get; }
    }
}
