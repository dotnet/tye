// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    internal static class KubernetesManifestGenerator
    {
        public static ServiceOutput CreateService(
            OutputContext output,
            ApplicationBuilder application,
            ProjectServiceBuilder project,
            DeploymentManifestInfo deployment,
            ServiceManifestInfo service)
        {
            var root = new YamlMappingNode();

            root.Add("kind", "Service");
            root.Add("apiVersion", "v1");

            var metadata = new YamlMappingNode();
            root.Add("metadata", metadata);
            metadata.Add("name", project.Name);

            if (service.Annotations.Count > 0)
            {
                var annotations = new YamlMappingNode();
                metadata.Add("annotations", annotations);

                foreach (var annotation in service.Annotations)
                {
                    annotations.Add(annotation.Key, new YamlScalarNode(annotation.Value) { Style = ScalarStyle.SingleQuoted, });
                }
            }

            var labels = new YamlMappingNode();
            metadata.Add("labels", labels);
            foreach (var label in service.Labels)
            {
                labels.Add(label.Key, new YamlScalarNode(label.Value) { Style = ScalarStyle.SingleQuoted, });
            }

            var spec = new YamlMappingNode();
            root.Add("spec", spec);

            var selector = new YamlMappingNode();
            spec.Add("selector", selector);

            // We need the name so we can use it with selector.
            if (!deployment.Labels.TryGetValue("app.kubernetes.io/name", out var selectorLabelValue))
            {
                throw new InvalidOperationException("The label 'app.kubernetes.io/name` is required.");
            }
            selector.Add("app.kubernetes.io/name", selectorLabelValue);

            spec.Add("type", "ClusterIP");

            var ports = new YamlSequenceNode();
            spec.Add("ports", ports);

            // We figure out the port based on bindings
            foreach (var binding in project.Bindings)
            {
                if (binding.Protocol == "https")
                {
                    // We skip https for now in deployment, because the E2E requires certificates
                    // and we haven't done those features yet.
                    continue;
                }

                if (binding.Port != null)
                {
                    var port = new YamlMappingNode();
                    ports.Add(port);

                    port.Add("name", binding.Name ?? binding.Protocol ?? "http");
                    port.Add("protocol", "TCP"); // we use assume TCP. YOLO
                    port.Add("port", binding.Port.Value.ToString());
                    port.Add("targetPort", binding.Port.Value.ToString());
                }
            }

            return new KubernetesServiceOutput(project.Name, new YamlDocument(root));
        }

        public static ServiceOutput CreateDeployment(
            OutputContext output,
            ApplicationBuilder application,
            ProjectServiceBuilder project,
            DeploymentManifestInfo deployment)
        {
            var bindings = project.Outputs.OfType<ComputedBindings>().FirstOrDefault();

            var root = new YamlMappingNode();

            root.Add("kind", "Deployment");
            root.Add("apiVersion", "apps/v1");

            var metadata = new YamlMappingNode();
            root.Add("metadata", metadata);
            metadata.Add("name", project.Name);

            if (deployment.Annotations.Count > 0)
            {
                var annotations = new YamlMappingNode();
                metadata.Add("annotations", annotations);

                foreach (var annotation in deployment.Annotations)
                {
                    annotations.Add(annotation.Key, new YamlScalarNode(annotation.Value) { Style = ScalarStyle.SingleQuoted, });
                }
            }

            var labels = new YamlMappingNode();
            metadata.Add("labels", labels);
            foreach (var label in deployment.Labels)
            {
                labels.Add(label.Key, new YamlScalarNode(label.Value) { Style = ScalarStyle.SingleQuoted, });
            }

            var spec = new YamlMappingNode();
            root.Add("spec", spec);

            spec.Add("replicas", project.Replicas.ToString());

            var selector = new YamlMappingNode();
            spec.Add("selector", selector);

            var matchLabels = new YamlMappingNode();
            selector.Add("matchLabels", matchLabels);

            // We need the name so we can use it with matchLabels.
            if (!deployment.Labels.TryGetValue("app.kubernetes.io/name", out var matchLabelsLabelValue))
            {
                throw new InvalidOperationException("The label 'app.kubernetes.io/name` is required.");
            }
            matchLabels.Add("app.kubernetes.io/name", matchLabelsLabelValue);

            var template = new YamlMappingNode();
            spec.Add("template", template);

            metadata = new YamlMappingNode();
            template.Add("metadata", metadata);

            if (deployment.Annotations.Count > 0)
            {
                var annotations = new YamlMappingNode();
                metadata.Add("annotations", annotations);

                foreach (var annotation in deployment.Annotations)
                {
                    annotations.Add(annotation.Key, new YamlScalarNode(annotation.Value) { Style = ScalarStyle.SingleQuoted, });
                }
            }

            labels = new YamlMappingNode();
            metadata.Add("labels", labels);
            foreach (var label in deployment.Labels)
            {
                labels.Add(label.Key, new YamlScalarNode(label.Value) { Style = ScalarStyle.SingleQuoted, });
            }

            spec = new YamlMappingNode();
            template.Add("spec", spec);

            var containers = new YamlSequenceNode();
            spec.Add("containers", containers);

            var images = project.Outputs.OfType<DockerImageOutput>();
            foreach (var image in images)
            {
                var container = new YamlMappingNode();
                containers.Add(container);
                container.Add("name", project.Name); // NOTE: to really support multiple images we'd need to generate unique names.
                container.Add("image", $"{image.ImageName}:{image.ImageTag}");
                container.Add("imagePullPolicy", "Always"); // helps avoid problems with development + weak versioning

                if (project.EnvironmentVariables.Count > 0 ||

                    // We generate ASPNETCORE_URLS if there are bindings for http
                    project.Bindings.Any(b => b.Protocol == "http" || b.Protocol is null) ||

                    // We generate environment variables for other services if there dependencies
                    (bindings is object && bindings.Bindings.OfType<EnvironmentVariableInputBinding>().Any()))
                {
                    var env = new YamlSequenceNode();
                    container.Add("env", env);

                    foreach (var kvp in project.EnvironmentVariables)
                    {
                        env.Add(new YamlMappingNode()
                        {
                            { "name", kvp.Name },
                            { "value", new YamlScalarNode(kvp.Value) { Style = ScalarStyle.SingleQuoted, } },
                        });
                    }

                    if (bindings is object)
                    {
                        foreach (var binding in bindings.Bindings.OfType<EnvironmentVariableInputBinding>())
                        {
                            env.Add(new YamlMappingNode()
                            {
                                { "name", binding.Name },
                                { "value", new YamlScalarNode(binding.Value) { Style = ScalarStyle.SingleQuoted, } },
                            });
                        }
                    }
                }

                if (bindings is object && bindings.Bindings.OfType<SecretInputBinding>().Any())
                {
                    var volumeMounts = new YamlSequenceNode();
                    container.Add("volumeMounts", volumeMounts);

                    foreach (var binding in bindings.Bindings.OfType<SecretInputBinding>())
                    {
                        var volumeMount = new YamlMappingNode();
                        volumeMounts.Add(volumeMount);

                        volumeMount.Add("name", $"{binding.Service.Name}-{binding.Binding.Name ?? binding.Service.Name}");
                        volumeMount.Add("mountPath", $"/var/tye/bindings/{binding.Service.Name}-{binding.Binding.Name ?? binding.Service.Name}");
                        volumeMount.Add("readOnly", "true");
                    }
                }

                if (project.Bindings.Count > 0)
                {
                    var ports = new YamlSequenceNode();
                    container.Add("ports", ports);

                    foreach (var binding in project.Bindings)
                    {
                        if (binding.Protocol == "https")
                        {
                            // We skip https for now in deployment, because the E2E requires certificates
                            // and we haven't done those features yet.
                            continue;
                        }

                        if (binding.Port != null)
                        {
                            var containerPort = new YamlMappingNode();
                            ports.Add(containerPort);
                            containerPort.Add("containerPort", binding.Port.Value.ToString());
                        }
                    }
                }
            }

            if (bindings.Bindings.OfType<SecretInputBinding>().Any())
            {
                var volumes = new YamlSequenceNode();
                spec.Add("volumes", volumes);

                foreach (var binding in bindings.Bindings.OfType<SecretInputBinding>())
                {
                    var volume = new YamlMappingNode();
                    volumes.Add(volume);
                    volume.Add("name", $"{binding.Service.Name}-{binding.Binding.Name ?? binding.Service.Name}");

                    var secret = new YamlMappingNode();
                    volume.Add("secret", secret);
                    secret.Add("secretName", binding.Name!);

                    var items = new YamlSequenceNode();
                    secret.Add("items", items);

                    var item = new YamlMappingNode();
                    items.Add(item);
                    item.Add("key", "connectionstring");
                    item.Add("path", binding.Filename);
                }
            }

            return new KubernetesDeploymentOutput(project.Name, new YamlDocument(root));
        }
    }
}
