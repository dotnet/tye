// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using k8s;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

        public static string ConvertToJson(string yamlContent)
        {
            var deserializer = new DeserializerBuilder().Build();
            var yamlObject = deserializer.Deserialize<string>(yamlContent);

            var serializer = new SerializerBuilder()
                .JsonCompatible()
                .Build();

            var json = serializer.Serialize(yamlObject);

            return json;
        }
    }
}
