// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace Tye
{
    internal static class OamComponentGenerator
    {
        public static OamComponentOutput CreateOamComponent(OutputContext output, Application application, ServiceEntry service)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (service is null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var root = new YamlMappingNode();

            root.Add("kind", "ComponentSchematic");
            root.Add("apiVersion", "core.oam.dev/v1alpha1");

            var metadata = new YamlMappingNode();
            root.Add("metadata", metadata);
            metadata.Add("name", service.Service.Name);

            var spec = new YamlMappingNode();
            root.Add("spec", spec);
            spec.Add("workloadType", "core.oam.dev/v1alpha1.Server");

            var containers = new YamlSequenceNode();
            spec.Add("containers", containers);

            var images = service.Outputs.OfType<DockerImageOutput>();
            foreach (var image in images)
            {
                var container = new YamlMappingNode();
                containers.Add(container);
                container.Add("name", service.Service.Name); // NOTE: to really support multiple images we'd need to generate unique names.
                container.Add("image", $"{image.ImageName}:{image.ImageTag}");
            }

            return new OamComponentOutput(service.Service.Name, new YamlDocument(root));
        }
    }
}
