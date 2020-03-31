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
                        continue;
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
                        // Don't support other types.
                        continue;
                }

                switch (key)
                {
                    case "name":
                        service.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "external":
                        if (!bool.TryParse(YamlParser.GetScalarValue(key, child.Value), out var external))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"external\" must be a boolean value (true/false).");
                        }
                        service.External = external;
                        break;
                    case "project":
                        service.Project = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "image":
                        service.Image = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "args":
                        service.Args = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "build":
                        if (!bool.TryParse(YamlParser.GetScalarValue(key, child.Value), out var build))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"build\" must be a boolean value (true/false).");
                        }
                        service.Build = build;
                        break;
                    case "executable":
                        service.Executable = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "workingdirectory":
                        service.WorkingDirectory = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "replicas":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var replicas))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"replicas\" value must be an integer.");
                        }

                        if (replicas < 0)
                        {
                            throw new TyeYamlException(child.Value.Start, "\"replicas\" value cannot be negative.");
                        }

                        service.Replicas = replicas;
                        break;
                    case "bindings":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, $"Excpeted yaml sequence for key: {key}.");
                        }

                        HandleServiceBindings((child.Value as YamlSequenceNode)!, service.Bindings);
                        break;
                    default:
                        continue;
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
                        break;
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
                        continue;
                }

                switch (key)
                {
                    case "name":
                        binding.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "connectionString":
                        binding.ConnectionString = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "autoAssignPort":
                        if (!bool.TryParse(YamlParser.GetScalarValue(key, child.Value), out var autoAssignPort))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"autoAssignPort\" must be a boolean value (true/false).");
                        }

                        binding.AutoAssignPort = autoAssignPort;
                        break;
                    case "port":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var port))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"port\" value must be an integer.");
                        }

                        binding.Port = port;
                        break;
                    case "containerPort":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var containerPort))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"containerPort\" value must be an integer.");
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
                        break;
                }
            }
        }
    }
}
