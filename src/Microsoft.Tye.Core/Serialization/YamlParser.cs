// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Tye.ConfigModel;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Tye.Serialization
{
    public class YamlParser : IDisposable
    {
        private YamlStream _yamlStream;
        private FileInfo? _fileInfo;
        private TextReader _reader;

        public YamlParser(string yamlContent, FileInfo? fileInfo = null)
            : this(new StringReader(yamlContent), fileInfo)
        {
        }

        public YamlParser(FileInfo fileInfo)
            : this(fileInfo.OpenText(), fileInfo)
        {
        }

        internal YamlParser(TextReader reader, FileInfo? fileInfo = null)
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
                if (_fileInfo != null)
                {
                    throw new TyeYamlException(ex.Start, $"Unable to parse '{_fileInfo.Name}'. See inner exception.", ex, _fileInfo);
                }

                throw new TyeYamlException(ex.Start, $"Unable to parse YAML.  See inner exception.", ex);
            }

            var app = new ConfigApplication();

            // TODO assuming first document.
            var document = _yamlStream.Documents[0];
            var node = document.RootNode;
            ThrowIfNotYamlMapping(node, _fileInfo);

            app.Source = _fileInfo!;

            ConfigApplicationParser.HandleConfigApplication((YamlMappingNode)node, app);

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

        public static Dictionary<string, object> GetDictionary(YamlNode node)
        {
            if (node.NodeType != YamlNodeType.Mapping)
            {
                throw new TyeYamlException(node.Start,
                    CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), node.NodeType.ToString()));
            }

            var dictionary = new Dictionary<string, object>();

            foreach (var mapping in (YamlMappingNode)node)
            {
                var key = YamlParser.GetScalarValue(mapping.Key);

                dictionary[key] = mapping.Value.NodeType switch
                {
                    YamlNodeType.Scalar => YamlParser.GetScalarValue(key, mapping.Value)!,
                    YamlNodeType.Mapping => YamlParser.GetDictionary(mapping.Value),
                    _ => throw new TyeYamlException(mapping.Value.Start,
                            CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), mapping.Value.NodeType.ToString()))
                };
            }

            return dictionary;
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

        public static void ThrowIfNotYamlMapping(YamlNode node, FileInfo? fileInfo = null)
        {
            if (node.NodeType != YamlNodeType.Mapping)
            {
                if (fileInfo != null)
                {
                    throw new TyeYamlException(node.Start,
                        CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), node.NodeType.ToString()), null, fileInfo);
                }
                throw new TyeYamlException(node.Start,
                    CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), node.NodeType.ToString()));
            }
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
