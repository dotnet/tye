// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Tye.ConfigModel;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public static class ConfigServiceParser
    {
        public static void HandleServiceMapping(YamlSequenceNode yamlSequenceNode, List<ConfigService> services)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                YamlParser.ThrowIfNotYamlMapping(child);
                var service = new ConfigService();
                HandleServiceNameMapping((YamlMappingNode)child, service);
                services.Add(service);
            }
        }

        private static void HandleServiceNameMapping(YamlMappingNode yamlMappingNode, ConfigService service)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        service.Name = YamlParser.GetScalarValue(key, child.Value).ToLowerInvariant();
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
                    case "dockerFile":
                        service.DockerFile = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "dockerFileArgs":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence(key));
                        }

                        HandleDockerFileArgs((child.Value as YamlSequenceNode)!, service.DockerFileArgs);
                        break;
                    case "dockerFileContext":
                        service.DockerFileContext = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "project":
                        service.Project = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "buildProperties":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence(key));
                        }

                        HandleBuildProperties((child.Value as YamlSequenceNode)!, service.BuildProperties);
                        break;
                    case "include":
                        service.Include = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "repository":
                        service.Repository = YamlParser.GetScalarValue(key, child.Value);
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
                    case "liveness":
                        service.Liveness = new ConfigProbe();
                        HandleServiceProbe((YamlMappingNode)child.Value, service.Liveness!);
                        break;
                    case "readiness":
                        service.Readiness = new ConfigProbe();
                        HandleServiceProbe((YamlMappingNode)child.Value, service.Readiness!);
                        break;
                    case "tags":
                        if (child.Value.NodeType != YamlNodeType.Sequence)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence(key));
                        }

                        HandleServiceTags((child.Value as YamlSequenceNode)!, service.Tags);
                        break;
                    case "azureFunction":
                        service.AzureFunction = YamlParser.GetScalarValue(key, child.Value);
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
                YamlParser.ThrowIfNotYamlMapping(child);
                var binding = new ConfigServiceBinding();
                HandleServiceBindingNameMapping((YamlMappingNode)child, binding);
                bindings.Add(binding);
            }
        }

        private static void HandleServiceBindingNameMapping(YamlMappingNode yamlMappingNode, ConfigServiceBinding binding)
        {
            foreach (var child in yamlMappingNode.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

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
                YamlParser.ThrowIfNotYamlMapping(child);
                var volume = new ConfigVolume();
                HandleServiceVolumeNameMapping((YamlMappingNode)child, volume);
                volumes.Add(volume);
            }
        }

        private static void HandleServiceProbe(YamlMappingNode yamlMappingNode, ConfigProbe probe)
        {
            foreach (var child in yamlMappingNode.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "http":
                        probe.Http = new ConfigHttpProber();
                        HandleServiceHttpProber((YamlMappingNode)child.Value, probe.Http!);
                        break;
                    case "initialDelay":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var initialDelay))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        if (initialDelay < 0)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBePositive(key));
                        }

                        probe.InitialDelay = initialDelay;
                        break;
                    case "period":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var period))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        if (period < 1)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeGreaterThanZero(key));
                        }

                        probe.Period = period;
                        break;
                    case "timeout":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var timeout))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        if (timeout < 1)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeGreaterThanZero(key));
                        }

                        probe.Timeout = timeout;
                        break;
                    case "successThreshold":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var successThreshold))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        if (successThreshold < 1)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeGreaterThanZero(key));
                        }

                        probe.SuccessThreshold = successThreshold;
                        break;
                    case "failureThreshold":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var failureThreshold))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        if (failureThreshold < 1)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeGreaterThanZero(key));
                        }

                        probe.FailureThreshold = failureThreshold;
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleServiceHttpProber(YamlMappingNode yamlMappingNode, ConfigHttpProber prober)
        {
            foreach (var child in yamlMappingNode.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "path":
                        prober.Path = YamlParser.GetScalarValue("path", child.Value);
                        break;
                    case "port":
                        if (!int.TryParse(YamlParser.GetScalarValue(key, child.Value), out var port))
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatMustBeAnInteger(key));
                        }

                        prober.Port = port;
                        break;
                    case "protocol":
                        prober.Path = YamlParser.GetScalarValue("protocol", child.Value);
                        break;
                    case "headers":
                        prober.Headers = new List<KeyValuePair<string, object>>();
                        var headersNode = child.Value as YamlSequenceNode;
                        if (headersNode is null)
                        {
                            throw new TyeYamlException(child.Value.Start, CoreStrings.FormatExpectedYamlSequence("headers"));
                        }

                        foreach (var header in headersNode.Children)
                        {
                            HandleServiceProbeHttpHeader((YamlMappingNode)header, prober.Headers);
                        }

                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleServiceProbeHttpHeader(YamlMappingNode yamlMappingNode, List<KeyValuePair<string, object>> headers)
        {
            string? name = null;
            object? value = null;

            foreach (var child in yamlMappingNode.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        name = YamlParser.GetScalarValue("name", child.Value);
                        break;
                    case "value":
                        value = YamlParser.GetScalarValue("value", child.Value);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }

            if (name is null)
            {
                throw new TyeYamlException(yamlMappingNode.Start, CoreStrings.FormatExpectedYamlScalar("name"));
            }
            else if (value is null)
            {
                throw new TyeYamlException(yamlMappingNode.Start, CoreStrings.FormatExpectedYamlScalar("value"));
            }

            headers.Add(new KeyValuePair<string, object>(name, value));
        }

        private static void HandleServiceVolumeNameMapping(YamlMappingNode yamlMappingNode, ConfigVolume volume)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

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

        private static void HandleBuildProperties(YamlSequenceNode yamlSequenceNode, List<BuildProperty> buildProperties)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                YamlParser.ThrowIfNotYamlMapping(child);
                var buildProperty = new BuildProperty();
                HandleServiceBuildPropertyNameMapping((YamlMappingNode)child, buildProperty);
                buildProperties.Add(buildProperty);
            }
        }
        private static void HandleDockerFileArgs(YamlSequenceNode yamlSequenceNode, Dictionary<string, string> dockerArguments)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                YamlParser.ThrowIfNotYamlMapping(child);
                HandleServiceDockerArgsNameMapping((YamlMappingNode)child, dockerArguments);
            }
        }

        private static void HandleServiceConfiguration(YamlSequenceNode yamlSequenceNode, List<ConfigConfigurationSource> configuration)
        {
            foreach (var child in yamlSequenceNode.Children)
            {
                YamlParser.ThrowIfNotYamlMapping(child);
                var config = new ConfigConfigurationSource();
                HandleServiceConfigurationNameMapping((YamlMappingNode)child, config);
                configuration.Add(config);
            }
        }

        private static void HandleServiceConfigurationNameMapping(YamlMappingNode yamlMappingNode, ConfigConfigurationSource config)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

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

        private static void HandleServiceBuildPropertyNameMapping(YamlMappingNode yamlMappingNode, BuildProperty buildProperty)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "name":
                        buildProperty.Name = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    case "value":
                        buildProperty.Value = YamlParser.GetScalarValue(key, child.Value);
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void HandleServiceTags(YamlSequenceNode yamlSequenceNode, List<string> tags)
        {
            foreach (var child in yamlSequenceNode!.Children)
            {
                var tag = YamlParser.GetScalarValue(child);
                tags.Add(tag);
            }
        }
        private static void HandleServiceDockerArgsNameMapping(YamlMappingNode yamlMappingNode, IDictionary<string, string> dockerArguments)
        {
            foreach (var child in yamlMappingNode!.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);
                var value = YamlParser.GetScalarValue(key, child.Value);

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }

                dockerArguments.Add(key, value);
            }
        }
    }
}
