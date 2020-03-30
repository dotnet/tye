// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Microsoft.Tye.Serialization
{
    public static class YamlSerializer
    {
        public static ISerializer CreateSerializer()
        {
            return new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                    .WithEmissionPhaseObjectGraphVisitor(args => new OmitDefaultAndEmptyArrayObjectGraphVisitor(args.InnerVisitor))
                    .Build();
        }

        public static IDeserializer CreateDeserializer()
        {
            return new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithNodeDeserializer(inner => new ValidatingNodeDeserializer(inner), s => s.InsteadOf<ObjectNodeDeserializer>())
                .Build();
        }

        // First, we'll implement a new INodeDeserializer
        // that will decorate another INodeDeserializer with validation:
        public class ValidatingNodeDeserializer : INodeDeserializer
        {
            private readonly INodeDeserializer _nodeDeserializer;

            public ValidatingNodeDeserializer(INodeDeserializer nodeDeserializer)
            {
                _nodeDeserializer = nodeDeserializer;
            }

            public bool Deserialize(IParser parser, Type expectedType, Func<IParser, Type, object?> nestedObjectDeserializer, out object value)
            {
                value = null!;
                if (_nodeDeserializer.Deserialize(parser, expectedType, nestedObjectDeserializer, out value!))
                {
                    var context = new ValidationContext(value, null, null);
                    Validator.ValidateObject(value, context, true);
                    return true;
                }
              
                return false;
            }
        }
    }
}
