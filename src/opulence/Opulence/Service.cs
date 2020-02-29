using System;
using System.Collections.Generic;

namespace Opulence
{
    public sealed class Service
    {
        public Service(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
        }

        public string Name { get; }

        public GeneratedAssets GeneratedAssets { get; } = new GeneratedAssets();

        public int? Port { get; set; }

        public string? Protocol { get; set; }

        public Source? Source { get; set; }

        public Dictionary<string, object> Environment { get; set; } = new Dictionary<string, object>();

        public int Replicas { get; set; } = 1;

        public List<ServiceBinding> Bindings { get; } = new List<ServiceBinding>();
    }
}
