using System.Collections.Generic;

namespace Microsoft.Tye
{
    public abstract class ServiceBuilder
    {
        public ServiceBuilder(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public List<BindingBuilder> Bindings { get; } = new List<BindingBuilder>();

        // TODO: this is temporary while refactoring
        public List<ServiceOutput> Outputs { get; } = new List<ServiceOutput>();
    }
}
