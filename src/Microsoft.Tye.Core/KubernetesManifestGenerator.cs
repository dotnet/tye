// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    internal static class KubernetesManifestGenerator
    {
        public static KubernetesIngressOutput CreateIngress(
            OutputContext output,
            ApplicationBuilder application,
            IngressBuilder ingress)
        {
            var root = new YamlMappingNode();

            root.Add("kind", "Ingress");
            root.Add("apiVersion", "extensions/v1beta1");

            var metadata = new YamlMappingNode();
            root.Add("metadata", metadata);
            metadata.Add("name", ingress.Name);
            if (!string.IsNullOrEmpty(application.Namespace))
            {
                metadata.Add("namespace", application.Namespace);
            }

            var annotations = new YamlMappingNode();
            metadata.Add("annotations", annotations);
            annotations.Add("kubernetes.io/ingress.class", new YamlScalarNode("nginx") { Style = ScalarStyle.SingleQuoted, });
            annotations.Add("nginx.ingress.kubernetes.io/rewrite-target", new YamlScalarNode("/$2") { Style = ScalarStyle.SingleQuoted, });

            var labels = new YamlMappingNode();
            metadata.Add("labels", labels);
            labels.Add("app.kubernetes.io/part-of", new YamlScalarNode(application.Name) { Style = ScalarStyle.SingleQuoted, });

            var spec = new YamlMappingNode();
            root.Add("spec", spec);

            if (ingress.Rules.Count > 0)
            {
                var rules = new YamlSequenceNode();
                spec.Add("rules", rules);

                // k8s ingress is grouped by host first, then grouped by path
                foreach (var hostgroup in ingress.Rules.GroupBy(r => r.Host))
                {
                    var rule = new YamlMappingNode();
                    rules.Add(rule);

                    if (!string.IsNullOrEmpty(hostgroup.Key))
                    {
                        rule.Add("host", hostgroup.Key);
                    }

                    var http = new YamlMappingNode();
                    rule.Add("http", http);

                    var paths = new YamlSequenceNode();
                    http.Add("paths", paths);

                    foreach (var ingressRule in hostgroup)
                    {
                        var path = new YamlMappingNode();
                        paths.Add(path);

                        var backend = new YamlMappingNode();
                        path.Add("backend", backend);
                        backend.Add("serviceName", ingressRule.Service);

                        var service = application.Services.FirstOrDefault(s => s.Name == ingressRule.Service);
                        if (service is null)
                        {
                            throw new InvalidOperationException($"Could not resolve service '{ingressRule.Service}'.");
                        }

                        var binding = service.Bindings.FirstOrDefault(b => b.Name is null || b.Name == "http");
                        if (binding is null)
                        {
                            throw new InvalidOperationException($"Could not resolve an http binding for service '{service.Name}'.");
                        }

                        backend.Add("servicePort", (binding.Port ?? 80).ToString(CultureInfo.InvariantCulture));

                        // Tye implements path matching similar to this example:
                        // https://kubernetes.github.io/ingress-nginx/examples/rewrite/
                        //
                        // Therefore our rewrite-target is set to $2 - we want to make sure we have
                        // two capture groups.
                        if (string.IsNullOrEmpty(ingressRule.Path) || ingressRule.Path == "/" || ingressRule.PreservePath)
                        {
                            path.Add("path", "/()(.*)"); // () is an empty capture group.
                        }
                        else
                        {
                            var regex = $"{ingressRule.Path.TrimEnd('/')}(/|$)(.*)";
                            path.Add("path", regex);
                        }
                    }
                }
            }

            return new KubernetesIngressOutput(ingress.Name, new YamlDocument(root));
        }

        public static KubernetesServiceOutput CreateService(
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

            if (!string.IsNullOrEmpty(application.Namespace))
            {
                metadata.Add("namespace", application.Namespace);
            }

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
                    port.Add("targetPort", (binding.ContainerPort ?? binding.Port.Value).ToString());
                }
            }

            return new KubernetesServiceOutput(project.Name, new YamlDocument(root));
        }

        public static KubernetesDeploymentOutput CreateDeployment(
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
            if (!string.IsNullOrEmpty(application.Namespace))
            {
                metadata.Add("namespace", application.Namespace);
            }

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

            if (project.Sidecars.Count > 0)
            {
                // Share process namespace when we have sidecars. So we can list other processes.
                // see: https://kubernetes.io/docs/tasks/configure-pod-container/share-process-namespace/#understanding-process-namespace-sharing
                spec.Add("shareProcessNamespace", new YamlScalarNode("true") { Style = ScalarStyle.Plain });
            }

            if (project.RelocateDiagnosticsDomainSockets)
            {
                // Our diagnostics functionality uses $TMPDIR to locate other dotnet processes through
                // eventpipe. see: https://github.com/dotnet/diagnostics/blob/master/documentation/design-docs/ipc-protocol.md#transport
                //
                // In order for diagnostics features to 'find' each other, we need to make $TMPDIR into
                // something shared.
                //
                // see: https://kubernetes.io/docs/tasks/access-application-cluster/communicate-containers-same-pod-shared-volume/
                project.EnvironmentVariables.Add(new EnvironmentVariableBuilder("TMPDIR")
                {
                    Value = "/var/tye/diagnostics",
                });

                foreach (var sidecar in project.Sidecars)
                {
                    sidecar.EnvironmentVariables.Add(new EnvironmentVariableBuilder("TMPDIR")
                    {
                        Value = "/var/tye/diagnostics",
                    });
                }
            }

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
                    bindings?.Bindings.Count > 0)
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
                        AddEnvironmentVariablesForComputedBindings(env, bindings);
                    }

                    if (project.RelocateDiagnosticsDomainSockets)
                    {
                        // volumeMounts:
                        // - name: shared-data
                        //   mountPath: /usr/share/nginx/html
                        var volumeMounts = new YamlSequenceNode();
                        container.Add("volumeMounts", volumeMounts);

                        var volumeMount = new YamlMappingNode();
                        volumeMounts.Add(volumeMount);
                        volumeMount.Add("name", "tye-diagnostics");
                        volumeMount.Add("mountPath", "/var/tye/diagnostics");
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
                            containerPort.Add("containerPort", (binding.ContainerPort ?? binding.Port.Value).ToString());
                        }
                    }
                }

                if (project.Liveness != null)
                {
                    AddProbe(project, container, project.Liveness!, "livenessProbe");
                }

                if (project.Readiness != null)
                {
                    AddProbe(project, container, project.Readiness!, "readinessProbe");
                }
            }

            foreach (var sidecar in project.Sidecars)
            {
                var container = new YamlMappingNode();
                containers.Add(container);
                container.Add("name", sidecar.Name); // NOTE: to really support multiple images we'd need to generate unique names.
                container.Add("image", $"{sidecar.ImageName}:{sidecar.ImageTag}");
                container.Add("imagePullPolicy", "Always"); // helps avoid problems with development + weak versioning

                if (sidecar.Args.Count > 0)
                {
                    var args = new YamlSequenceNode();
                    container.Add("args", args);

                    foreach (var arg in sidecar.Args)
                    {
                        args.Add(new YamlScalarNode(arg) { Style = ScalarStyle.SingleQuoted, });
                    }
                }

                var sidecarBindings = sidecar.Outputs.OfType<ComputedBindings>().FirstOrDefault();
                if (sidecar.EnvironmentVariables.Count > 0 || sidecarBindings?.Bindings.Count > 0)
                {
                    var env = new YamlSequenceNode();
                    container.Add("env", env);

                    foreach (var kvp in sidecar.EnvironmentVariables)
                    {
                        env.Add(new YamlMappingNode()
                        {
                            { "name", kvp.Name },
                            { "value", new YamlScalarNode(kvp.Value) { Style = ScalarStyle.SingleQuoted, } },
                        });
                    }

                    if (sidecarBindings is object)
                    {
                        AddEnvironmentVariablesForComputedBindings(env, sidecarBindings);
                    }
                }

                if (project.RelocateDiagnosticsDomainSockets)
                {
                    // volumeMounts:
                    // - name: shared-data
                    //   mountPath: /usr/share/nginx/html
                    var volumeMounts = new YamlSequenceNode();
                    container.Add("volumeMounts", volumeMounts);

                    var volumeMount = new YamlMappingNode();
                    volumeMounts.Add(volumeMount);
                    volumeMount.Add("name", "tye-diagnostics");
                    volumeMount.Add("mountPath", "/var/tye/diagnostics");
                }
            }

            if (project.RelocateDiagnosticsDomainSockets)
            {
                // volumes:
                // - name: shared-data
                //   emptyDir: {}
                var volumes = new YamlSequenceNode();
                spec.Add("volumes", volumes);

                var volume = new YamlMappingNode();
                volumes.Add(volume);
                volume.Add("name", "tye-diagnostics");
                volume.Add("emptyDir", new YamlMappingNode());
            }

            return new KubernetesDeploymentOutput(project.Name, new YamlDocument(root));
        }

        private static void AddProbe(ServiceBuilder service, YamlMappingNode container, ProbeBuilder builder, string name)
        {
            var probe = new YamlMappingNode();
            container.Add(name, probe);

            if (builder.Http != null)
            {
                var builderHttp = builder.Http;
                var httpGet = new YamlMappingNode();

                probe.Add("httpGet", httpGet);
                httpGet.Add("path", builderHttp.Path);

                if (builderHttp.Protocol != null)
                {
                    httpGet.Add("scheme", builderHttp.Protocol.ToUpper());
                }

                if (builderHttp.Port != null)
                {
                    httpGet.Add("port", builderHttp.Port.ToString()!);
                }
                else
                {
                    // If port is not given, we pull default port
                    var binding = service.Bindings.First(b => builderHttp.Protocol == null || b.Protocol == builderHttp.Protocol);
                    if (binding.Port != null)
                    {
                        httpGet.Add("port", binding.Port.Value.ToString());
                    }

                    if (builderHttp.Protocol == null && binding.Protocol != null)
                    {
                        httpGet.Add("scheme", binding.Protocol.ToUpper());
                    }
                }

                if (builderHttp.Headers.Count > 0)
                {
                    var headers = new YamlSequenceNode();
                    httpGet.Add("httpHeaders", headers);

                    foreach (var builderHeader in builderHttp.Headers)
                    {
                        var header = new YamlMappingNode();
                        header.Add("name", builderHeader.Key);
                        header.Add("value", builderHeader.Value.ToString()!);
                        headers.Add(header);
                    }
                }
            }

            probe.Add("initialDelaySeconds", builder.InitialDelay.ToString());
            probe.Add("periodSeconds", builder.Period.ToString()!);
            probe.Add("successThreshold", builder.SuccessThreshold.ToString()!);
            probe.Add("failureThreshold", builder.FailureThreshold.ToString()!);
        }

        private static void AddEnvironmentVariablesForComputedBindings(YamlSequenceNode env, ComputedBindings bindings)
        {
            foreach (var binding in bindings.Bindings.OfType<EnvironmentVariableInputBinding>())
            {
                env.Add(new YamlMappingNode()
                {
                    { "name", binding.Name },
                    { "value", new YamlScalarNode(binding.Value) { Style = ScalarStyle.SingleQuoted, } },
                });
            }

            foreach (var binding in bindings.Bindings.OfType<SecretInputBinding>())
            {
                //- name: SECRET_USERNAME
                //  valueFrom:
                //    secretKeyRef:
                //      name: mysecret
                //      key: username

                if (binding is SecretConnectionStringInputBinding connectionStringBinding)
                {
                    AddSecret(env, connectionStringBinding.KeyName, binding.Name, "connectionstring");

                }
                else if (binding is SecretUrlInputBinding urlBinding)
                {
                    AddSecret(env, $"{urlBinding.KeyNameBase}__PROTOCOL", binding.Name, "protocol");
                    AddSecret(env, $"{urlBinding.KeyNameBase}__HOST", binding.Name, "host");
                    AddSecret(env, $"{urlBinding.KeyNameBase}__PORT", binding.Name, "port");
                }
            }

            static void AddSecret(YamlSequenceNode env, string name, string secret, string key)
            {
                env.Add(new YamlMappingNode()
                {
                    { "name", name },
                    {
                        "valueFrom", new YamlMappingNode()
                        {
                            {
                                "secretKeyRef", new YamlMappingNode()
                                {
                                    { "name", new YamlScalarNode(secret) { Style = ScalarStyle.SingleQuoted } },
                                    { "key", new YamlScalarNode(key) { Style = ScalarStyle.SingleQuoted } },
                                }
                            },
                        }
                    },
                });
            }
        }
    }
}
