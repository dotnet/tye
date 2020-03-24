// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
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
        public SecretInputBinding(string name, string filename, ServiceBuilder service, BindingBuilder binding)
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
        public ServiceBuilder Service { get; }
        public BindingBuilder Binding { get; }
    }
}
