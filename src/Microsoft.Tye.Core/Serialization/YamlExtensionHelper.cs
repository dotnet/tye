// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.Tye.ConfigModel;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public static class YamlExtensionHelper
    {
        internal static void HandleExtensionsMapping(YamlSequenceNode yamlSequenceNode, List<Dictionary<string, object>> extensions)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                switch (child.NodeType)
                {
                    case YamlNodeType.Mapping:
                        var extensionDictionary = new Dictionary<string, object>();
                        foreach (var mapping in (child as YamlMappingNode)!)
                        {
                            string? key;
                            switch (mapping.Key.NodeType)
                            {
                                case YamlNodeType.Scalar:
                                    key = (mapping.Key as YamlScalarNode)!.Value;
                                    break;
                                default:
                                    throw new TyeYamlException(mapping.Key.Start,
                                        CoreStrings.FormatUnexpectedType(YamlNodeType.Scalar.ToString(), mapping.Key.NodeType.ToString()));
                            }

                            if (key != null)
                            {
                                extensionDictionary[key] = YamlParser.GetScalarValue(key, mapping.Value)!;
                            }
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
