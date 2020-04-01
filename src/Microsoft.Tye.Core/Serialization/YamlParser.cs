// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Tye.ConfigModel;
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
            : this (fileInfo.OpenText())
        {
            _fileInfo = fileInfo;
        }

        public YamlParser(TextReader reader)
        {
            _reader = reader;
            _yamlStream = new YamlStream();
            _yamlStream.Load(reader);
        }

        public ConfigApplication ParseConfigApplication()
        {
            var app = new ConfigApplication();

            // TODO assuming first document.
            var document = _yamlStream.Documents[0];
            var node = document.RootNode;
            switch (node.NodeType)
            {
                case YamlNodeType.Mapping:
                    YamlConfigApplicationHelpers.HandleConfigApplication(node as YamlMappingNode, app);
                    break;
                default:
                    throw new TyeYamlException(node.Start, 
                        CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), node.NodeType.ToString()));
            }

            app.Source = _fileInfo!;

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

        public static string? GetScalarValue(string key, YamlNode node)
        {
            if (node.NodeType != YamlNodeType.Scalar)
            {
                throw new TyeYamlException(node.Start, CoreStrings.FormatExpectedYamlScalar(key));
            }

            return (node as YamlScalarNode)!.Value;
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
