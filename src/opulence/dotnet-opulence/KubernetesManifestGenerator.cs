using System;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace Opulence
{
    internal static class KubernetesManifestGenerator
    {
        public static ServiceOutput CreateService(OutputContext output, Application application, ServiceEntry service)
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

            root.Add("kind", "Service");
            root.Add("apiVersion", "v1");

            var metadata = new YamlMappingNode();
            root.Add("metadata", metadata);
            metadata.Add("name", service.Service.Name);

            var labels = new YamlMappingNode();
            metadata.Add("labels", labels);
            labels.Add("app.kubernetes.io/name", service.Service.Name);
            labels.Add("app.kubernetes.io/part-of", application.Globals.Name);

            var spec = new YamlMappingNode();
            root.Add("spec", spec);

            var selector = new YamlMappingNode();
            spec.Add("selector", selector);
            selector.Add("app.kubernetes.io/name", service.Service.Name);

            spec.Add("type", "ClusterIP");

            var ports = new YamlSequenceNode();
            spec.Add("ports", ports);

            var port = new YamlMappingNode();
            ports.Add(port);

            port.Add("name", "web");
            port.Add("protocol", "TCP");
            port.Add("port", "80");
            port.Add("targetPort", "80");

            return new KubernetesServiceOutput(service.Service.Name, new YamlDocument(root));
        }

        public static ServiceOutput CreateDeployment(OutputContext output, Application application, ServiceEntry service)
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

            root.Add("kind", "Deployment");
            root.Add("apiVersion", "apps/v1");

            var metadata = new YamlMappingNode();
            root.Add("metadata", metadata);
            metadata.Add("name", service.Service.Name);

            var labels = new YamlMappingNode();
            metadata.Add("labels", labels);
            labels.Add("app.kubernetes.io/name", service.Service.Name);
            labels.Add("app.kubernetes.io/part-of", application.Globals.Name);

            var spec = new YamlMappingNode();
            root.Add("spec", spec);

            var selector = new YamlMappingNode();
            spec.Add("selector", selector);

            var matchLabels = new YamlMappingNode();
            selector.Add("matchLabels", matchLabels);
            matchLabels.Add("app.kubernetes.io/name", service.Service.Name);

            var template = new YamlMappingNode();
            spec.Add("template", template);

            metadata = new YamlMappingNode();
            template.Add("metadata", metadata);

            labels = new YamlMappingNode();
            metadata.Add("labels", labels);
            labels.Add("app.kubernetes.io/name", service.Service.Name);
            labels.Add("app.kubernetes.io/part-of", application.Globals.Name);

            spec = new YamlMappingNode();
            template.Add("spec", spec);

            var containers = new YamlSequenceNode();
            spec.Add("containers", containers);

            var images = service.Outputs.OfType<DockerImageOutput>();
            foreach (var image in images)
            {
                var container = new YamlMappingNode();
                containers.Add(container);
                container.Add("name", service.Service.Name); // NOTE: to really support multiple images we'd need to generate unique names.
                container.Add("image", $"{image.ImageName}:{image.ImageTag}");

                if (service.Service.Environment.Count > 0)
                {
                    var env = new YamlSequenceNode();
                    container.Add("env", env);

                    foreach (var kvp in service.Service.Environment)
                    {
                        env.Add(new YamlMappingNode()
                        {
                            { "name", kvp.Key },
                            { "value", kvp.Value.ToString() },
                        });
                    }
                }

                if (service.Service.Bindings.Any(b => b.ConnectionString != null))
                {
                    var volumeMounts = new YamlSequenceNode();
                    container.Add("volumeMounts", volumeMounts);

                    foreach (var binding in service.Service.Bindings.Where(b => b.ConnectionString != null))
                    {
                        var volumeMount = new YamlMappingNode();
                        volumeMounts.Add(volumeMount);

                        volumeMount.Add("name", $"{binding.Name}-secret");
                        volumeMount.Add("mountPath", $"/var/bindings/{binding.Name}");
                        volumeMount.Add("readOnly", "true");
                    }
                }

                var ports = new YamlSequenceNode();
                container.Add("ports", ports);

                var containerPort = new YamlMappingNode();
                ports.Add(containerPort);
                containerPort.Add("containerPort", "80");
            }

            if (service.Service.Bindings.Any(b => b.ConnectionString != null))
            {
                var volumes = new YamlSequenceNode();
                spec.Add("volumes", volumes);

                foreach (var binding in service.Service.Bindings.Where(b => b.ConnectionString != null))
                {
                    var volume = new YamlMappingNode();
                    volumes.Add(volume);
                    volume.Add("name", $"{binding.Name}-secret");

                    var secret = new YamlMappingNode();
                    volume.Add("secret", secret);
                    secret.Add("secretName", binding.ConnectionString!.Name!);

                    var items = new YamlSequenceNode();
                    secret.Add("items", items);

                    var item = new YamlMappingNode();
                    items.Add(item);
                    item.Add("key", "uri");
                    item.Add("path", $"SERVICES__{binding.Name}");
                }
            }

            return new KubernetesDeploymentOutput(service.Service.Name, new YamlDocument(root));
        }
    }
}
