// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Tye.ConfigModel;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public static class YamlIngressHelpers
    {
        public static void HandleIngress(YamlSequenceNode yamlSequenceNode, List<ConfigIngress> ingress)
        {
            foreach (var s in yamlSequenceNode.Children)
            {
                var configIngress = new ConfigIngress();
                switch (s.NodeType)
                {
                    case YamlNodeType.Mapping:
                        HandleIngressMapping(s as YamlMappingNode, configIngress);
                        break;
                    default:
                        continue;
                }
            }
        }

        private static void HandleIngressMapping(YamlMappingNode? yamlMappingNode, ConfigIngress configIngress)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                string key;
                switch (child.Key.NodeType)
                {
                    case YamlNodeType.Scalar:
                        key = (child.Key as YamlScalarNode)!.Value!;
                        break;
                    default:
                        // Don't support other types.
                        continue;
                }

                switch (key)
                {
                    case "name":
                        configIngress.Name = GetScalarValue(key, child.Value);
                        break;
                    case "replicas":
                        var value = GetScalarValue(key, child.Value);
                        if (!int.TryParse(value, out var replicas))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"replicas\" value must be an integer.");
                        }

                        if (replicas < 0)
                        {
                            throw new TyeYamlException(child.Value.Start, "\"replicas\" value cannot be negative.");
                        }

                        configIngress.Replicas = replicas;
                        break;
                    case "rules":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, $"Excpeted yaml sequence for key: {key}.");
                        }
                        HandleIngressRules((child.Value as YamlSequenceNode)!, configIngress.Rules);
                        break;
                    case "bindings":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, $"Excpeted yaml sequence for key: {key}.");
                        }
                        HandleIngressBindings((child.Value as YamlSequenceNode)!, configIngress.Bindings);
                        break;
                    default:
                        continue;
                }
            }
        }

        private static void HandleIngressRules(YamlSequenceNode yamlSequenceNode, List<ConfigIngressRule> bindings)
        {
            foreach (var s in yamlSequenceNode.Children)
            {
                var rule = new ConfigIngressRule();
                switch (s.NodeType)
                {
                    case YamlNodeType.Mapping:
                        // get key and value here
                        HandleIngressRuleMapping(s as YamlMappingNode, rule);
                        break;
                    default:
                        continue;

                }

                bindings.Add(rule);
            }
        }

        private static void HandleIngressRuleMapping(YamlMappingNode? yamlMappingNode, ConfigIngressRule rule)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                string? key;
                switch (child.Key.NodeType)
                {
                    case YamlNodeType.Scalar:
                        key = (child.Key as YamlScalarNode)!.Value;
                        break;
                    default:
                        // Don't support other types.
                        continue;
                }

                switch (key)
                {
                    case "host":
                        rule.Host = GetScalarValue(key, child.Value);
                        break;
                    case "path":
                        rule.Path = GetScalarValue(key, child.Value);
                        break;
                    case "port":
                        rule.Service = GetScalarValue(key, child.Value);
                        break;
                    default:
                        break;
                }
            }
        }

        private static void HandleIngressBindings(YamlSequenceNode yamlSequenceNode, List<ConfigIngressBinding> bindings)
        {
            foreach (var s in yamlSequenceNode.Children)
            {
                switch (s.NodeType)
                {
                    case YamlNodeType.Mapping:
                        // get key and value here
                        var binding = new ConfigIngressBinding();
                        HandleIngressBindingMapping(s as YamlMappingNode, binding);
                        bindings.Add(binding);
                        break;
                    default:
                        continue;
                }
            }
        }

        private static void HandleIngressBindingMapping(YamlMappingNode? yamlMappingNode, ConfigIngressBinding binding)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                string? key;
                switch (child.Key.NodeType)
                {
                    case YamlNodeType.Scalar:
                        key = (child.Key as YamlScalarNode)!.Value;
                        break;
                    default:
                        // Don't support other types.
                        continue;
                }

                switch (key)
                {
                    case "name":
                        binding.Name = GetScalarValue(key, child.Value);
                        break;
                    case "autoAssignPort":
                        if (!bool.TryParse(GetScalarValue(key, child.Value), out var autoAssignPort))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"autoAssignPort\" must be a boolean value (true/false).");
                        }

                        binding.AutoAssignPort = autoAssignPort;
                        break;
                    case "port":
                        if (!int.TryParse(GetScalarValue(key, child.Value), out var port))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"port\" value must be an integer.");
                        }

                        binding.Port = port;
                        break;
                    case "protocol":
                        binding.Protocol = GetScalarValue(key, child.Value);
                        break;
                    default:
                        continue;
                }
            }
        }
    }
}
