// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Tye.ConfigModel;
using Tye.Serialization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Tye.DockerCompose
{
    public class DockerComposeParser : IDisposable
    {
        private YamlStream _yamlStream;
        private FileInfo? _fileInfo;
        private TextReader _reader;

        public DockerComposeParser(string yamlContent, FileInfo? fileInfo = null)
            : this(new StringReader(yamlContent), fileInfo)
        {
        }

        public DockerComposeParser(FileInfo fileInfo)
            : this(fileInfo.OpenText(), fileInfo)
        {
        }

        internal DockerComposeParser(TextReader reader, FileInfo? fileInfo = null)
        {
            _reader = reader;
            _yamlStream = new YamlStream();
            _fileInfo = fileInfo;
        }

        public ConfigApplication ParseConfigApplication()
        {
            try
            {
                _yamlStream.Load(_reader);
            }
            catch (YamlException ex)
            {
                throw new TyeYamlException(ex.Start, "Unable to parse tye.yaml. See inner exception.", ex);
            }

            var app = new ConfigApplication();

            // TODO assuming first document.
            var document = _yamlStream.Documents[0];
            var node = document.RootNode;
            ThrowIfNotYamlMapping(node);

            app.Source = _fileInfo!;

            Parse((YamlMappingNode)node, app);

            app.Name ??= NameInferer.InferApplicationName(_fileInfo!);

            // TODO confirm if these are ever null.
            foreach (var service in app.Services)
            {
                service.Bindings ??= new List<ConfigServiceBinding>();
                service.Configuration ??= new List<ConfigConfigurationSource>();
                service.Volumes ??= new List<ConfigVolume>();
                service.Tags ??= new List<string>();
            }

            foreach (var ingress in app.Ingress)
            {
                ingress.Bindings ??= new List<ConfigIngressBinding>();
                ingress.Rules ??= new List<ConfigIngressRule>();
                ingress.Tags ??= new List<string>();
            }

            return app;
        }

        public static string GetScalarValue(YamlNode node)
        {
            if (node.NodeType != YamlNodeType.Scalar)
            {
                throw new TyeYamlException(node.Start,
                    CoreStrings.FormatUnexpectedType(YamlNodeType.Scalar.ToString(), node.NodeType.ToString()));
            }

            return ((YamlScalarNode)node).Value!;
        }

        public static string GetScalarValue(string key, YamlNode node)
        {
            if (node.NodeType != YamlNodeType.Scalar)
            {
                throw new TyeYamlException(node.Start, CoreStrings.FormatExpectedYamlScalar(key));
            }

            return ((YamlScalarNode)node).Value!;
        }

        public static void ThrowIfNotYamlSequence(string key, YamlNode node)
        {
            if (node.NodeType != YamlNodeType.Sequence)
            {
                throw new TyeYamlException(node.Start, CoreStrings.FormatExpectedYamlSequence(key));
            }
        }

        public static void ThrowIfNotYamlMapping(YamlNode node)
        {
            if (node.NodeType != YamlNodeType.Mapping)
            {
                throw new TyeYamlException(node.Start,
                    CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), node.NodeType.ToString()));
            }
        }

        public void Dispose()
        {
            _reader.Dispose();
        }

        internal static void Parse(YamlMappingNode node, ConfigApplication app)
        {
            foreach (var child in node.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "version":
                        break;
                    case "volumes":
                        break;
                    case "services":
                        ParseServiceList((child.Value as YamlMappingNode)!, app);
                        break;
                    case "networks":
                        break;
                    case "configs":
                        break;
                    case "secrets":
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void ParseServiceList(YamlMappingNode node, ConfigApplication app)
        {
            foreach (var child in node.Children)
            {
                var service = new ConfigService();
                service.Name = YamlParser.GetScalarValue(child.Key);
                ParseService((child.Value as YamlMappingNode)!, service);
                app.Services.Add(service);
            }
        }

        private static void ParseService(YamlMappingNode node, ConfigService service)
        {
            foreach (var child in node.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "build":
                        ParseBuild((child.Value as YamlMappingNode)!, service);
                        break;
                    case "cap_add":
                        break;
                    case "cap_drop":
                        break;
                    case "cgroup_parent":
                        break;
                    case "command":
                        break;
                    case "configs":
                        break;
                    case "container_name":
                        break;
                    case "credential_spec":
                        break;
                    case "depends_on":
                        break;
                    case "deploy":
                        break;
                    case "devices":
                        break;
                    case "dns":
                        break;
                    case "dns_search":
                        break;
                    case "endpoint":
                        break;
                    case "env_file":
                        break;
                    case "environment":
                        ParseEnvironment(child.Value, service);
                        break;
                    case "expose":
                        break;
                    case "external_links":
                        break;
                    case "extra_hosts":
                        break;
                    case "healthcheck":
                        break;
                    case "image":
                        service.Image = YamlParser.GetScalarValue(child.Value);
                        break;
                    case "init":
                        break;
                    case "isolation":
                        break;
                    case "labels":
                        break;
                    case "links":
                        break;
                    case "logging":
                        break;
                    case "network_mode":
                        break;
                    case "networks":
                        break;
                    case "pid":
                        break;
                    case "ports":
                        ParsePortSequence((child.Value as YamlSequenceNode)!, service);
                        break;
                    case "restart":
                        break;
                    case "secrets":
                        break;
                    case "security_opt":
                        break;
                    case "stop_grace_period":
                        break;
                    case "stop_signal":
                        break;
                    case "sysctls":
                        break;
                    case "tmpfs":
                        break;
                    case "ulimits":
                        break;
                    case "userns_mode":
                        break;
                    case "volumes":
                        break;
                    case "user":
                        break;
                    case "working_dir":
                        break;
                    case "domainname":
                        break;
                    case "hostname":
                        break;
                    case "ipc":
                        break;
                    case "mac_address":
                        break;
                    case "privileged":
                        break;
                    case "read_only":
                        break;
                    case "shm_size":
                        break;
                    case "stdin_open":
                        break;
                    case "tty":
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void ParseEnvironment(YamlNode node, ConfigService service)
        {
            if (node is YamlSequenceNode sequenceNode)
            {
                foreach (var arg in sequenceNode)
                {
                    var configItem = new ConfigConfigurationSource();
                    var argString = YamlParser.GetScalarValue(arg);
                    if (argString.Contains('='))
                    {
                        var parts = argString.Split('=');
                        configItem.Name = parts[0];
                        configItem.Value = parts[1];
                    }
                    else
                    {
                        configItem.Name = argString;
                    }
                    service.Configuration.Add(configItem);
                }
            }
            else
            {
                var mappingNode = (node as YamlMappingNode)!;
                foreach (var arg in mappingNode)
                {
                    var configItem = new ConfigConfigurationSource();
                    configItem.Name = YamlParser.GetScalarValue(arg.Key);
                    configItem.Value = YamlParser.GetScalarValue(arg.Value);

                    service.Configuration.Add(configItem);
                }
            }
            
        }

        private static void ParsePortSequence(YamlSequenceNode portSequence, ConfigService service)
        {
            foreach (var port in portSequence)
            {
                var portString = YamlParser.GetScalarValue(port);
                var binding = new ConfigServiceBinding();
                if (portString.Contains(':'))
                {
                    var ports = portString.Split(':');
                    binding.Port = int.Parse(ports[0]);
                    binding.ContainerPort = int.Parse(ports[1]);
                }
                else
                {
                    binding.Port = int.Parse(portString);
                    binding.ContainerPort = int.Parse(portString);
                }

                // TODO how to specify protocol with docker compose. Using http for now.
                binding.Protocol = "http";
                service.Bindings.Add(binding);
            }
        }

        private static void ParseVolumes(YamlMappingNode node, ConfigApplication app)
        {
            foreach (var child in node.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "driver":
                        break;
                    case "driver_opts":
                        break;
                    case "external":
                        break;
                    case "labels":
                        break;
                    case "name":
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static void ParseNetworks(YamlMappingNode node, ConfigApplication app)
        {
            foreach (var child in node.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "driver":
                        break;
                    case "driver_opts":
                        break;
                    case "attachable":
                        break;
                    case "enable_ipv6":
                        break;
                    case "ipam":
                        break;
                    case "internal":
                        break;
                    case "external":
                        break;
                    case "labels":
                        break;
                    case "name":
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        private static readonly string[] FileFormats = new[] { "*.csproj", "*.fsproj"};

        // Build seems like it would just work, context would just point to the csproj if no dockerfile is present.
        private static void ParseBuild(YamlMappingNode node, ConfigService service)
        {
            foreach (var child in node.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "context":
                        // Potentially find project or context based on that.
                        // could potentially specify a project here instead?
                        var folder = new DirectoryInfo(YamlParser.GetScalarValue(child.Value));
                        foreach (var format in FileFormats)
                        {
                            var projs = Directory.GetFiles(folder.FullName, format);
                            if (projs.Length == 1)
                            {
                                service.Project = projs[0];
                                break;
                            }
                            if (projs.Length > 1)
                            {
                                throw new TyeYamlException("Multiple proj files found in directory, have only a single proj file in the context directory.");
                            }
                        }
                        // check if folder has proj file, and use that.
                        break;
                    case "dockerfile":
                        service.DockerFile = YamlParser.GetScalarValue(child.Value);
                        break;
                    case "args":
                        //service.Configuration = ParseDockerBuildArgs((child.Value as YamlSequenceNode)!, service);
                        break;
                    case "cache_from":
                        break;
                    case "labels":
                        break;
                    case "network":
                        break;
                    case "shm_size":
                        break;
                    case "target":
                        break;
                    default:
                        throw new TyeYamlException(child.Key.Start, CoreStrings.FormatUnrecognizedKey(key));
                }
            }
        }

        //private static List<ConfigConfigurationSource> ParseDockerBuildArgs(YamlSequenceNode node, ConfigService service)
        //{

        //}
    }
}
