using System;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Tye
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

            if (application.Globals.Name is object)
            {
                labels.Add("app.kubernetes.io/part-of", application.Globals.Name);
            }

            var spec = new YamlMappingNode();
            root.Add("spec", spec);

            var selector = new YamlMappingNode();
            spec.Add("selector", selector);
            selector.Add("app.kubernetes.io/name", service.Service.Name);

            spec.Add("type", "ClusterIP");

            var ports = new YamlSequenceNode();
            spec.Add("ports", ports);

            // We figure out the port based on bindings
            foreach (var binding in service.Service.Bindings)
            {
                var port = new YamlMappingNode();
                ports.Add(port);

                port.Add("name", binding.Name ?? "web");
                port.Add("protocol", "TCP"); // we use assume TCP. YOLO
                port.Add("port", binding.Port?.ToString() ?? "80");
                port.Add("targetPort", binding.Port?.ToString() ?? "80");
            }

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

            var bindings = service.Outputs.OfType<ComputedBindings>().FirstOrDefault();

            var root = new YamlMappingNode();

            root.Add("kind", "Deployment");
            root.Add("apiVersion", "apps/v1");

            var metadata = new YamlMappingNode();
            root.Add("metadata", metadata);
            metadata.Add("name", service.Service.Name);

            var labels = new YamlMappingNode();
            metadata.Add("labels", labels);
            labels.Add("app.kubernetes.io/name", service.Service.Name);
            if (application.Globals.Name is object)
            {
                labels.Add("app.kubernetes.io/part-of", application.Globals.Name);
            }

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
            if (application.Globals.Name is object)
            {
                labels.Add("app.kubernetes.io/part-of", application.Globals.Name);
            }

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
                container.Add("imagePullPolicy", "Always"); // helps avoid problems with development + weak versioning

                if (service.Service.Environment.Count > 0 ||

                    // We generate ASPNETCORE_URLS if there are bindings for http
                    service.Service.Bindings.Any(b => b.Protocol == "http" || b.Protocol is null) ||

                    // We generate environment variables for other services if there dependencies
                    (bindings is object && bindings.Bindings.OfType<EnvironmentVariableInputBinding>().Any()))
                {
                    var env = new YamlSequenceNode();
                    container.Add("env", env);

                    foreach (var kvp in service.Service.Environment)
                    {
                        env.Add(new YamlMappingNode()
                        {
                            { "name", kvp.Key },
                            { "value", new YamlScalarNode(kvp.Value.ToString()) { Style = ScalarStyle.SingleQuoted, } },
                        });
                    }

                    foreach (var binding in service.Service.Bindings)
                    {
                        if (binding.Protocol == "http" || binding.Protocol == null)
                        {
                            var port = binding.Port ?? 80;
                            env.Add(new YamlMappingNode()
                            {
                                { "name", "ASPNETCORE_URLS" },
                                { "value", $"http://*{(binding.Port == 80 ? "" : (":" + binding.Port.ToString()))}" },
                            });
                        }
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

                        volumeMount.Add("name", $"{binding.Service.Service.Name}-{binding.Binding.Name}");
                        volumeMount.Add("mountPath", $"/var/tye/bindings/{binding.Service.Service.Name}-{binding.Binding.Name}");
                        volumeMount.Add("readOnly", "true");
                    }
                }

                if (service.Service.Bindings.Count > 0)
                {
                    var ports = new YamlSequenceNode();
                    container.Add("ports", ports);

                    foreach (var binding in service.Service.Bindings)
                    {
                        var containerPort = new YamlMappingNode();
                        ports.Add(containerPort);
                        containerPort.Add("containerPort", binding.Port?.ToString() ?? "80");
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
                    volume.Add("name", $"{binding.Service.Service.Name}-{binding.Binding.Name}");

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

            return new KubernetesDeploymentOutput(service.Service.Name, new YamlDocument(root));
        }
    }
}
