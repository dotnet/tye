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

        private static void ParseService(YamlMappingNode node, ConfigApplication app)
        {
            foreach (var child in node.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "build":
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
        // Build seems like it would just work, context would just point to the csproj if no dockerfile is present.
        private static void ParseBuild(YamlMappingNode node, ConfigApplication app)
        {
            foreach (var child in node.Children)
            {
                var key = YamlParser.GetScalarValue(child.Key);

                switch (key)
                {
                    case "context":
                        break;
                    case "dockerfile":
                        break;
                    case "args":
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
    }
}
