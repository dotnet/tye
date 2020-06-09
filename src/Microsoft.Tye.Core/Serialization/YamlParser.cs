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

        public YamlParser(string yamlContent)
            : this(new StringReader(yamlContent))
        {
        }

        public YamlParser(FileInfo fileInfo)
            : this(fileInfo.OpenText())
        {
            _fileInfo = fileInfo;
        }

        internal YamlParser(TextReader reader)
        {
            _reader = reader;
            _yamlStream = new YamlStream();
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
            ConfigApplicationParser.HandleConfigApplication((YamlMappingNode)node, app);

            app.Source = _fileInfo!;
            app.Name ??= NameInferer.InferApplicationName(_fileInfo!);

            // TODO confirm if these are ever null.
            foreach (var service in app.Services)
            {
                service.Bindings ??= new List<ConfigServiceBinding>();
                service.Configuration ??= new List<ConfigConfigurationSource>();
                service.Volumes ??= new List<ConfigVolume>();
            }

            foreach (var ingress in app.Ingress)
            {
                ingress.Bindings ??= new List<ConfigIngressBinding>();
                ingress.Rules ??= new List<ConfigIngressRule>();
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
    }
}
