using System.Collections.Generic;

namespace Opulence
{
    public class ComputedBindings : ServiceOutput
    {
        public List<InputBinding> Bindings { get; } = new List<InputBinding>();
    }

    public abstract class InputBinding
    {
    }

    public sealed class EnvironmentVariableInputBinding : InputBinding
    {
        public EnvironmentVariableInputBinding(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public string Value { get; }
    }

    public sealed class SecretInputBinding : InputBinding
    {
        public SecretInputBinding(string name, string filename, ServiceEntry service, ServiceBinding binding)
        {
            Name = name;
            Filename = filename;
            Service = service;
            Binding = binding;
        }

        // Used to generate a kubernetes secret
        public string Name { get; }
        public string? Value { get; }

        // Used to map the secret to a key that ASP.NET Core understandes
        public string Filename { get; }

        // Used for informational purposes
        public ServiceEntry Service { get; }
        public ServiceBinding Binding { get; }
    }
}
