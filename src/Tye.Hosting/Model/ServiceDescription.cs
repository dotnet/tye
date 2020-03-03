using System.Collections.Generic;

namespace Tye.Hosting.Model
{
    public class ServiceDescription
    {
        public ServiceDescription(string name, RunInfo? runInfo)
        {
            Name = name;
            RunInfo = runInfo;
        }

        public string Name { get; }

        public RunInfo? RunInfo { get; }

        public int Replicas { get; set; } = 1;
        public List<ServiceBinding> Bindings { get; } = new List<ServiceBinding>();
        public List<ConfigurationSource> Configuration { get; } = new List<ConfigurationSource>();
    }

    public class ConfigurationSource
    {
        public ConfigurationSource(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public string Value { get; }
    }
}
