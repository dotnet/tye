// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    internal static class OamApplicationGenerator
    {
        public static Task WriteOamApplicationAsync(TextWriter writer, OutputContext output, ApplicationBuilder application, string environment)
        {
            if (writer is null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (environment is null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            var componentManifests = new List<OamComponentOutput>();
            var documents = new List<YamlDocument>();

            foreach (var service in application.Services)
            {
                componentManifests.AddRange(service.Outputs.OfType<OamComponentOutput>());
            }

            var root = new YamlMappingNode();

            root.Add("kind", "ApplicationConfiguration");
            root.Add("apiVersion", "core.oam.dev/v1alpha1");

            var metadata = new YamlMappingNode();
            root.Add("metadata", metadata);
            metadata.Add("name", application.Name);

            var spec = new YamlMappingNode();
            root.Add("spec", spec);

            var components = new YamlSequenceNode();
            spec.Add("components", components);

            foreach (var manifest in componentManifests)
            {
                documents.Add(manifest.Yaml);

                var component = new YamlMappingNode();
                components.Add(component);

                component.Add("componentName", manifest.Name);
                component.Add("instanceName", $"{environment.ToLowerInvariant()}-{manifest.Name}");
            }

            documents.Add(new YamlDocument(root));

            var stream = new YamlStream(documents.ToArray());
            stream.Save(writer, assignAnchors: false);

            return Task.CompletedTask;
        }
    }
}
