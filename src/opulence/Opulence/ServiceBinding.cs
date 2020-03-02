using System;

namespace Opulence
{
    public class ServiceBinding
    {
        public ServiceBinding(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public string? Host { get; set; }
        public string? Protocol { get; set; }
        public int? Port { get; set; }
        public string? ConnectionString { get; set; }
        public Secret? Secret { get; set; }
    }
}
