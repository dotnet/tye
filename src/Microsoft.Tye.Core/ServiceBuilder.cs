using System.Collections.Generic;

namespace Microsoft.Tye
{
    public abstract class ServiceBuilder
    {
        public ServiceBuilder(string name, ServiceSource source)
        {
            Name = name;
            Source = source;
        }

        public string Name { get; }

        public List<BindingBuilder> Bindings { get; } = new List<BindingBuilder>();

        // TODO: this is temporary while refactoring
        public List<ServiceOutput> Outputs { get; } = new List<ServiceOutput>();

        public HashSet<string> Dependencies { get; } = new HashSet<string>();

        public ServiceSource Source { get; }
    }
}
