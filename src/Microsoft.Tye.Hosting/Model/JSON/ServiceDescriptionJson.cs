using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model
{
    public class ServiceDescriptionJson
    {
        public string? Name { get; set; }
        public int Replicas { get; set; } = 1;
        public List<ServiceBinding>? Bindings { get; set; }
        public List<ConfigurationSource>? Configuration { get; set;  }
    }
}
