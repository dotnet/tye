// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Rest;

namespace Microsoft.Tye
{
    public sealed class ValidateIngressStep : ApplicationExecutor.IngressStep
    {
        public override string DisplayText => "Validating Ingress...";

        public string Environment { get; set; } = "production";

        public bool Interactive { get; set; }

        public bool Force { get; set; }

        // Keep track of ingress classes we've seen so we don't validate them twice.
        public HashSet<string> IngressClasses { get; } = new HashSet<string>();

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application, IngressBuilder ingress)
        {
            // This code assumes that in the future we might support other ingress types besides nginx.
            //
            // Right now we only know some hardcoded details about ingress-nginx that we use for both 
            // validation and generation of manifests.
            //
            // For instance we don't support k8s 1.18.X IngressClass resources because that version
            // isn't broadly available yet. 
            if (Force)
            {
                output.WriteDebugLine("Skipping ingress validation because force was specified.");
                return;
            }

            var ingressClass = "nginx";
            if (!IngressClasses.Add(ingressClass))
            {
                output.WriteDebugLine($"Already validated ingress class '{ingressClass}'.");
                return;
            }

            if (await KubectlDetector.GetKubernetesServerVersion(output) == null)
            {
                throw new CommandException($"Cannot validate ingress because kubectl is not installed.");
            }

            output.WriteDebugLine($"Validating ingress class '{ingressClass}'.");
            var config = KubernetesClientConfiguration.BuildDefaultConfig();
            
            // If namespace is null, set it to default
            config.Namespace ??= "default";

            if (!await KubectlDetector.IsKubectlConnectedToClusterAsync(output, config.Namespace))
            {
                throw new CommandException($"Cannot validate ingress because kubectl is not connected to a cluster.");
            }

            var kubernetes = new Kubernetes(config);

            // Looking for a deployment using a standard label.
            // Note: using a deployment instead of a service - minikube doesn't create a service for the controller.

            try
            {
                var result = await kubernetes.ListDeploymentForAllNamespacesWithHttpMessagesAsync(
                    labelSelector: "app.kubernetes.io/name in (ingress-nginx, nginx-ingress-controller)");
                if (result.Body.Items.Count > 0)
                {
                    foreach (var service in result.Body.Items)
                    {
                        output.WriteInfoLine($"Found existing ingress controller '{service.Metadata.Name}' in namespace '{service.Metadata.NamespaceProperty}'.");
                    }

                    return;
                }
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                // The kubernetes client uses exceptions for 404s.
            }
            catch (Exception ex)
            {
                output.WriteDebugLine("Failed to query secret.");
                output.WriteDebugLine(ex.ToString());
                throw new CommandException("Unable connect to kubernetes.", ex);
            }

            if (!Interactive)
            {
                throw new CommandException(
                    $"An ingress was specified for the application, but the 'ingress-nginx' controller could not be found. " +
                    $"Rerun the command with --interactive to deploy a controller for development, or with --force to skip validation. Alternatively " +
                    $"see our documentation on ingress: https://aka.ms/tye/ingress");
            }

            output.WriteAlwaysLine(
                "Tye can deploy the ingress-nginx controller for you. This will be a basic deployment suitable for " +
                "experimentation and development. Your production needs, or requirements may differ depending on your Kubernetes distribution. " +
                "See: https://aka.ms/tye/ingress for documentation.");
            if (!output.Confirm($"Deploy ingress-nginx"))
            {
                // user skipped deployment of ingress, continue with deployment.
                return;
            }

            // We want to be able to detect minikube because the process for enabling nginx-ingress is different there,
            // it's shipped as an addon.
            // 
            // see: https://github.com/telepresenceio/telepresence/blob/4364fd83d5926bef46babd704e7bd6c82a75dbd6/telepresence/startup.py#L220
            if (config.CurrentContext == "minikube")
            {
                output.WriteDebugLine($"Running 'minikube addons enable ingress'");
                output.WriteCommandLine("minikube", "addon enable ingress");
                var capture = output.Capture();
                var exitCode = await ProcessUtil.ExecuteAsync(
                    $"minikube",
                    $"addons enable ingress",
                    System.Environment.CurrentDirectory,
                    stdOut: capture.StdOut,
                    stdErr: capture.StdErr);

                output.WriteDebugLine($"Done running 'minikube addons enable ingress' exit code: {exitCode}");
                if (exitCode != 0)
                {
                    throw new CommandException("'minikube addons enable ingress' failed.");
                }

                output.WriteInfoLine($"Deployed ingress-nginx.");
            }
            else
            {
                // If we get here then we should deploy the ingress controller.

                // The first time we apply the ingress controller, the validating webhook will not have started.
                // This causes an error to be returned from the process. As this always happens, we are going to
                // not check the error returned and assume the kubectl command worked. This is double checked in
                // the future as well when we try to create the ingress resource.

                output.WriteDebugLine($"Running 'kubectl apply'");
                output.WriteCommandLine("kubectl", $"apply -f \"https://aka.ms/tye/ingress/deploy\"");
                var capture = output.Capture();
                var exitCode = await ProcessUtil.ExecuteAsync(
                    $"kubectl",
                    $"apply -f \"https://aka.ms/tye/ingress/deploy\"",
                    System.Environment.CurrentDirectory);

                output.WriteDebugLine($"Done running 'kubectl apply' exit code: {exitCode}");

                output.WriteInfoLine($"Waiting for ingress-nginx controller to start.");

                // We need to then wait for the webhooks that are created by ingress-nginx to start. Deploying an ingress immediately
                // after creating the controller will fail if the webhook isn't ready.
                //
                // Internal error occurred: failed calling webhook "validate.nginx.ingress.kubernetes.io": 
                // Post https://ingress-nginx-controller-admission.ingress-nginx.svc:443/networking.k8s.io/v1/ingresses?timeout=30s:
                // dial tcp 10.0.31.130:443: connect: connection refused
                //
                // Unfortunately this is the likely case for us.

                try
                {
                    output.WriteDebugLine("Watching for ingress-nginx controller readiness...");
                    var response = await kubernetes.ListNamespacedPodWithHttpMessagesAsync(
                        namespaceParameter: "ingress-nginx",
                        labelSelector: "app.kubernetes.io/component=controller,app.kubernetes.io/name=ingress-nginx",
                        watch: true);

                    var tcs = new TaskCompletionSource<object?>();
                    using var watcher = response.Watch<V1Pod, V1PodList>(
                        onEvent: (@event, pod) =>
                        {
                            // Wait for the readiness-check to pass.
                            if (pod.Status.Conditions.All(c => string.Equals(c.Status, bool.TrueString, StringComparison.OrdinalIgnoreCase)))
                            {
                                tcs.TrySetResult(null); // Success!
                                output.WriteDebugLine($"Pod '{pod.Metadata.Name}' is ready.");
                            }
                        },
                        onError: ex =>
                        {
                            tcs.TrySetException(ex);
                            output.WriteDebugLine("Watch operation failed.");
                        },
                        onClosed: () =>
                        {
                            // YOLO?
                            tcs.TrySetResult(null);
                            output.WriteDebugLine("Watch operation completed.");
                        });

                    await tcs.Task;
                }
                catch (Exception ex)
                {
                    output.WriteDebugLine("Failed to ingress-nginx pods.");
                    output.WriteDebugLine(ex.ToString());
                    throw new CommandException("Failed to query ingress-nginx pods.", ex);
                }

                output.WriteInfoLine($"Deployed ingress-nginx.");
            }
        }
    }
}



