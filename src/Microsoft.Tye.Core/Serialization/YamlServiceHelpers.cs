// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Tye.ConfigModel;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public static class YamlServiceHelpers
    {
        public static void HandleServiceMapping(YamlSequenceNode yamlSequenceNode, List<ConfigService> services)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                var service = new ConfigService();
                switch (child.NodeType)
                {
                    case YamlNodeType.Mapping:
                        // get key and value here
                        HandleServiceNameMapping((child as YamlMappingNode)!, service);
                        break;
                    default:
                        throw new TyeYamlException(child.Start, 
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), child.NodeType.ToString()));
                }

                services.Add(service);
            }
        }

        private static void HandleServiceNameMapping(YamlMappingNode yamlMappingNode, ConfigService service)
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
                        throw new TyeYamlException(child.Key.Start,
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Scalar.ToString(), child.Key.NodeType.ToString()));
                }

                switch (key)
                {
                    case "name":
                        service.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "external":
                        if (!bool.TryParse(YamlParser.GetScalarValue(key, child.Value), out var external))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeABoolean(key));
                        }
                        service.External = external;
                        break;
                    case "image":
                        service.Image = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "project":
                        service.Project = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "build":
                        if (!bool.TryParse(YamlParser.GetScalarValue(key, child.Value), out var build))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeABoolean(key));
                        }
                        service.Build = build;
                        break;
                    case "executable":
                        service.Executable = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "workingDirectory":
                        service.WorkingDirectory = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "args":
                        service.Args = YamlParser.GetScalarValue(key, child.Value);
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

                        service.Replicas = replicas;
                        break;
                    case "bindings":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence(key));
                        }

                        HandleServiceBindings((child.Value as YamlSequenceNode)!, service.Bindings);
                        break;
                    case "volumes":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence(key));
                        }

                        HandleServiceVolumes((child.Value as YamlSequenceNode)!, service.Volumes);
                        break;
                    case "env":
                    case "configuration":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence(key));
                        }
                        HandleServiceConfiguration((child.Value as YamlSequenceNode)!, service.Configuration);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleServiceBindings(YamlSequenceNode yamlSequenceNode, List<ConfigServiceBinding> bindings)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                switch (child.NodeType)
                {
                    case YamlNodeType.Mapping:
                        var binding = new ConfigServiceBinding();
                        HandleServiceBindingNameMapping(child as YamlMappingNode, binding);
                        bindings.Add(binding);
                        break;
                    default:
                        throw new TyeYamlException(child.Start,
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), child.NodeType.ToString()));
                }
            }
        }

        private static void HandleServiceBindingNameMapping(YamlMappingNode? yamlMappingNode, ConfigServiceBinding binding)
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
                        throw new TyeYamlException(child.Key.Start,
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Scalar.ToString(), child.Key.NodeType.ToString()));
                }

                switch (key)
                {
                    case "name":
                        binding.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "connectionString":
                        binding.ConnectionString = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "port":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var port))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        binding.Port = port;
                        break;
                    case "containerPort":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var containerPort))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        binding.ContainerPort = containerPort;
                        break;
                    case "host":
                        binding.Host = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "protocol":
                        binding.Protocol = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleServiceVolumes(YamlSequenceNode yamlSequenceNode, List<ConfigVolume> volumes)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                switch (child.NodeType)
                {
                    case YamlNodeType.Mapping:
                        var volume = new ConfigVolume();
                        HandleServiceVolumeNameMapping(child as YamlMappingNode, volume);
                        volumes.Add(volume);
                        break;
                    default:
                        throw new TyeYamlException(child.Start,
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), child.NodeType.ToString()));
                }
            }
        }

        private static void HandleServiceVolumeNameMapping(YamlMappingNode? yamlMappingNode, ConfigVolume volume)
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
                        throw new TyeYamlException(child.Key.Start,
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Scalar.ToString(), child.Key.NodeType.ToString()));
                }

                switch (key)
                {
                    case "name":
                        volume.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "source":
                        volume.Source = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "target":
                        volume.Target = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleServiceConfiguration(YamlSequenceNode yamlSequenceNode, List<ConfigConfigurationSource> configuration)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                switch (child.NodeType)
                {
                    case YamlNodeType.Mapping:
                        var config = new ConfigConfigurationSource();
                        HandleServiceConfigurationNameMapping(child as YamlMappingNode, config);
                        configuration.Add(config);
                        break;
                    default:
                        throw new TyeYamlException(child.Start,
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), child.NodeType.ToString()));
                }
            }
        }

        private static void HandleServiceConfigurationNameMapping(YamlMappingNode? yamlMappingNode, ConfigConfigurationSource config)
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
                        throw new TyeYamlException(child.Key.Start,
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Scalar.ToString(), child.Key.NodeType.ToString()));
                }

                switch (key)
                {
                    case "name":
                        config.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "value":
                        config.Value = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }
    }
}
