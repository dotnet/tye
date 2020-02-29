using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace Micronetes.Hosting.Model
{
    public class ServiceDescription
    {
        public string Name { get; set; }
        public bool External { get; set; }
        public string DockerImage { get; set; }
        public string Project { get; set; }
        public bool? Build { get; set; } = true;
        public string Executable { get; set; }
        public string WorkingDirectory { get; set; }
        public string Args { get; set; }
        public int? Replicas { get; set; }
        public List<ServiceBinding> Bindings { get; set; } = new List<ServiceBinding>();
        [YamlMember(Alias = "env")]
        public List<ConfigurationSource> Configuration { get; set; } = new List<ConfigurationSource>();
    }

    public class ConfigurationSource
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Source { get; set; }
    }
}
