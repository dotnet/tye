using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.Tye.ConfigModel;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public class YamlParser
    {
        private YamlStream _yamlStream;

        public YamlParser(string yamlContent)
        {
            var stringReader = new StringReader(yamlContent);
            _yamlStream = new YamlStream();
            _yamlStream.Load(stringReader);
        }

        public ConfigApplication GetConfigApplication()
        {
            var app = new ConfigApplication();

            // TODO assuming that first document.
            var document = _yamlStream.Documents[0];
            foreach (var node in document.AllNodes)
            {
                switch (node.NodeType)
                {
                    case YamlNodeType.Mapping:
                        HandleMapping(node as YamlMappingNode, app);
                        break;
                    case YamlNodeType.Alias:
                        // Ignore alias
                        break;
                    case YamlNodeType.Scalar:
                        // Shouldn't have a scalar here
                        break;
                    case YamlNodeType.Sequence:
                        break;
                }
            }

            return app;
        }

        private void HandleMapping(YamlMappingNode? yamlMappingNode, ConfigApplication app)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                string key = null;
                string value = null;
                switch (child.Key.NodeType)
                {
                    case YamlNodeType.Scalar:
                        key = (child.Key as YamlScalarNode)!.Value;
                        break;
                    default:
                        // Don't support other types.
                        continue;
                }

                switch (child.Value.NodeType)
                {
                    case YamlNodeType.Mapping:
                        break;
                    case YamlNodeType.Alias:
                        break;
                    case YamlNodeType.Scalar:
                        value = (child.Value as YamlScalarNode)!.Value;
                        break;
                    case YamlNodeType.Sequence:
                        break;
                }

                switch (key)
                {
                    case "name":
                        app.Name = value;
                        break;
                    case "registry":
                        app.Registry = value;
                        break;
                    case "ingress":
                        // Handle ingress
                        // app.Ingress = 
                        break;
                    case "services":
                        // handle services
                        HanldeServiceMapping(child.Value as YamlSequenceNode, app.Services);
                        break;
                }
            }
        }

        private void HanldeServiceMapping(YamlSequenceNode? yamlSequenceNode, List<ConfigService> Services)
        {
            foreach (var s in yamlSequenceNode.Children)
            {
                var service = new ConfigService();
                switch (s.NodeType)
                {
                    case YamlNodeType.Mapping:
                        // get key and value here
                        HandleServiceNameMapping(s as YamlMappingNode, service);
                        break;
                    default:
                        continue;

                }
            }
        }

        private void HandleServiceNameMapping(YamlMappingNode? yamlMappingNode, ConfigService service)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                string key = null;
                string value = null;
                switch (child.Key.NodeType)
                {
                    case YamlNodeType.Scalar:
                        key = (child.Key as YamlScalarNode)!.Value;
                        break;
                    default:
                        // Don't support other types.
                        break;
                }

                switch (child.Value.NodeType)
                {
                    case YamlNodeType.Mapping:
                        break;
                    case YamlNodeType.Alias:
                        break;
                    case YamlNodeType.Scalar:
                        value = (child.Value as YamlScalarNode)!.Value;
                        break;
                    case YamlNodeType.Sequence:
                        break;
                }

                switch (key)
                {
                    case "name":
                        service.Name = value;
                        break;
                    case "external":
                        if (!bool.TryParse(value, out var external))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"external\" must be a boolean value (true/false).");
                        }
                        service.External = external;
                        break;
                    case "project":
                        service.Project = value;
                        break;
                    case "image":
                        service.Image = value;
                        break;
                    case "args":
                        service.Args = value;
                        break;
                    case "build":
                        if (!bool.TryParse(value, out var build))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"build\" must be a boolean value (true/false).");
                        }
                        service.Build = build;
                        break;
                    case "executable":
                        service.Executable = value;
                        break;
                    case "workingdirectory":
                        service.WorkingDirectory = value;
                        break;
                    case "replicas":
                        if (!int.TryParse(value, out var intRes))
                        {
                            throw new TyeYamlException(child.Value.Start, "\"replicas\" value must be an integer.");
                        }

                        if (intRes < 0)
                        {
                            throw new TyeYamlException(child.Value.Start, "\"replicas\" value cannot be negative.");
                        }

                        service.Replicas = intRes;
                        break;
                    case "bindings":
                        HandleBindings(child.Value as YamlSequenceNode, service.Bindings);
                        break;
                }
            }
        }

        private void HandleBindings(YamlSequenceNode yamlSequenceNode, List<ConfigServiceBinding> bindings)
        {
            foreach (var s in yamlSequenceNode.Children)
            {
                var binding = new ConfigServiceBinding();
                switch (s.NodeType)
                {
                    case YamlNodeType.Mapping:
                        // get key and value here
                        HandleServiceBindingNameMapping(s as YamlMappingNode, binding);
                        break;
                    default:
                        continue;

                }
            }
        }

        private void HandleServiceBindingNameMapping(YamlMappingNode? yamlMappingNode, ConfigServiceBinding binding)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                string key = null;
                string value = null;
                switch (child.Key.NodeType)
                {
                    case YamlNodeType.Scalar:
                        key = (child.Key as YamlScalarNode)!.Value;
                        break;
                    default:
                        // Don't support other types.
                        break;
                }

                switch (child.Value.NodeType)
                {
                    case YamlNodeType.Mapping:
                        break;
                    case YamlNodeType.Alias:
                        break;
                    case YamlNodeType.Scalar:
                        value = (child.Value as YamlScalarNode)!.Value;
                        break;
                    case YamlNodeType.Sequence:
                        break;
                }

                switch (key)
                {
                    case "name":
                        binding.Name = value;
                        break;
                    case "connectionstring":
                        binding.ConnectionString = value;
                        break;
                    case "autoassignport":
                        binding.AutoAssignPort = value;
                        break;
                    case "port":
                        binding.Port = value;
                        break;
                    case "containerport":
                        binding.ContainerPort = value;
                        break;
                    case "host":
                        binding.Host = build;
                        break;
                    case "protocol":
                        binding.Protocol = value;
                        break;
                }
            }
        }
    }
}
