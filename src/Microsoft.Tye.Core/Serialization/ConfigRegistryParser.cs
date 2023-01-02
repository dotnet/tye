// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Tye.ConfigModel;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public class ConfigRegistryParser
    {
        public static ConfigRegistry HandleRegistry(string key, YamlNode node)
        {
            ConfigRegistry configRegistry;
            if (node.NodeType == YamlNodeType.Scalar)
            {
                configRegistry = new ConfigRegistry
                {
                    Hostname = ((YamlScalarNode)node).Value!
                };
            }
            else if (node.NodeType == YamlNodeType.Mapping)
            {
                configRegistry = HandleRegistryMapping((YamlMappingNode)node);
            }
            else
            {
                throw new TyeYamlException(node.Start, CoreStrings.FormatExpectedYamlScalar(key));
            }

            return configRegistry;
        }

        private static ConfigRegistry HandleRegistryMapping(YamlMappingNode mappingNode)
        {
            var configRegistry = new ConfigRegistry();

            foreach (var child in mappingNode.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        configRegistry.Hostname = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "pullSecret":
                        configRegistry.PullSecret = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }

            return configRegistry;
        }
    }
}
