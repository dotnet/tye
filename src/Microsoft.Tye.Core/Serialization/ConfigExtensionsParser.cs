// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public static class ConfigExtensionsParser
    {
        public static void HandleExtensionsMapping(YamlSequenceNode yamlSequenceNode, List<Dictionary<string, object>> extensions)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                switch (child.NodeType)
                {
                    case YamlNodeType.Mapping:
                        var extensionDictionary = new Dictionary<string, object>();
                        foreach (var mapping in (YamlMappingNode)child)
                        {
                            var key = YamlParser.GetScalarValue(mapping.Key);
                            extensionDictionary[key] = YamlParser.GetScalarValue(key, mapping.Value)!;
                        }

                        extensions.Add(extensionDictionary);
                        break;
                    default:
                        throw new TyeYamlException(child.Start,
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), child.NodeType.ToString()));
                }
            }
        }
    }
}
