// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Tye.ConfigModel;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public static class ConfigIngressParser
    {
        public static void HandleIngress(YamlSequenceNode yamlSequenceNode, List<ConfigIngress> ingress)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                YamlParser.ThrowIfNotYamlMapping(child);
                var configIngress = new ConfigIngress();
                HandleIngressMapping((YamlMappingNode)child, configIngress);
                ingress.Add(configIngress);
            }
        }

        private static void HandleIngressMapping(YamlMappingNode yamlMappingNode, ConfigIngress configIngress)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        configIngress.Name = YamlParser.GetScalarValue(key, child.Value).ToLowerInvariant();
                        break;
                    case "replicas":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var replicas))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        if (replicas < 0)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBePositive(key));
                        }

                        configIngress.Replicas = replicas;
                        break;
                    case "rules":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence(key));
                        }
                        HandleIngressRules((child.Value as YamlSequenceNode)!, configIngress.Rules);
                        break;
                    case "bindings":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence(key));
                        }
                        HandleIngressBindings((child.Value as YamlSequenceNode)!, configIngress.Bindings);
                        break;
                    case "tags":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence(key));
                        }

                        HandleIngressTags((child.Value as YamlSequenceNode)!, configIngress.Tags);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleIngressRules(YamlSequenceNode yamlSequenceNode, List<ConfigIngressRule> rules)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                YamlParser.ThrowIfNotYamlMapping(child);
                var rule = new ConfigIngressRule();
                HandleIngressRuleMapping((YamlMappingNode)child, rule);
                rules.Add(rule);
            }
        }

        private static void HandleIngressRuleMapping(YamlMappingNode yamlMappingNode, ConfigIngressRule rule)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "host":
                        rule.Host = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "path":
                        rule.Path = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "preservePath":
                        if (!bool.TryParse(YamlParser.GetScalarValue(key, child.Value), out var preservePath))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeABoolean(key));
                        }
                        rule.PreservePath = preservePath;
                        break;
                    case "service":
                        rule.Service = YamlParser.GetScalarValue(key, child.Value).ToLowerInvariant();
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleIngressBindings(YamlSequenceNode yamlSequenceNode, List<ConfigIngressBinding> bindings)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                YamlParser.ThrowIfNotYamlMapping(child);
                var binding = new ConfigIngressBinding();
                HandleIngressBindingMapping((YamlMappingNode)child, binding);
                bindings.Add(binding);
            }
        }

        private static void HandleIngressBindingMapping(YamlMappingNode yamlMappingNode, ConfigIngressBinding binding)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        binding.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "port":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var port))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        binding.Port = port;
                        break;
                    case "ip":
                        if (YamlParser.GetScalarValue(key, child.Value) is string ipString
                            && (IPAddress.TryParse(ipString, out var ip) || ipString == "*" || ipString.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
                        {
                            binding.IPAddress = ipString;
                        }
                        else
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnIPAddress(key));
                        }
                        break;
                    case "protocol":
                        binding.Protocol = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleIngressTags(YamlSequenceNode yamlSequenceNode, List<string> tags)
        {
            foreach (var child in yamlSequenceNode!.Children)
            {
                var tag = YamlParser.GetScalarValue(child);
                tags.Add(tag);
            }
        }
    }
}
