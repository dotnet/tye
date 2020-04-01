// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Tye.ConfigModel;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public static class ConfigSecretsParser
    {
        public static void HandleConfigSecrets(YamlMappingNode yamlMappingNode, ConfigSecrets secrets)
        {
            foreach (var child in yamlMappingNode.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        secrets.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "providers":
                        YamlParser.ThrowIfNotYamlSequence(key, child.Value);
                        ConfigSecretProviderParser.HandleSecretProviders((child.Value as YamlSequenceNode)!, secrets.Providers);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }
    }

    public static class ConfigSecretProviderParser
    {
        public static void HandleSecretProviders(YamlSequenceNode yamlSequenceNode, List<ConfigSecretProvider> providers)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                YamlParser.ThrowIfNotYamlMapping(child);
                var configSecretProvider = new ConfigSecretProvider();
                HandleSecretProviderMapping((YamlMappingNode)child, configSecretProvider);
                providers.Add(configSecretProvider);
            }
        }

        private static void HandleSecretProviderMapping(YamlMappingNode yamlMappingNode, ConfigSecretProvider configSecretProvider)
        {
            foreach (var child in yamlMappingNode.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        configSecretProvider.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "type":
                        configSecretProvider.Type = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "settings":
                        YamlParser.ThrowIfNotYamlMapping(child.Value);
                        ConfigSecretProviderParser.HandleSettingsMapping((child.Value as YamlMappingNode)!, configSecretProvider.Settings);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleSettingsMapping(YamlMappingNode yamlSequenceNode, Dictionary<string, string> settings)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);
                var value = YamlParser.GetScalarValue(key, child.Value)!;
                settings[key] = value;
            }
        }
    }
}
