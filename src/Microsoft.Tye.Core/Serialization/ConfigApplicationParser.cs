// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Tye.ConfigModel;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public static class ConfigApplicationParser
    {
        public static void HandleConfigApplication(YamlMappingNode yamlMappingNode, ConfigApplication app)
        {
            foreach (var child in yamlMappingNode.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        app.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "namespace":
                        app.Namespace = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "network":
                        app.Network = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "registry":
                        app.Registry = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "ingress":
                        YamlParser.ThrowIfNotYamlSequence(key, child.Value);
                        ConfigIngressParser.HandleIngress((child.Value as YamlSequenceNode)!, app.Ingress);
                        break;
                    case "services":
                        YamlParser.ThrowIfNotYamlSequence(key, child.Value);
                        ConfigServiceParser.HandleServiceMapping((child.Value as YamlSequenceNode)!, app.Services, app);
                        break;
                    case "extensions":
                        YamlParser.ThrowIfNotYamlSequence(key, child.Value);
                        ConfigExtensionsParser.HandleExtensionsMapping((child.Value as YamlSequenceNode)!, app.Extensions);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }
    }
}
