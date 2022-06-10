// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Rest;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    public sealed class ValidateSecretStep : ApplicationExecutor.ServiceStep
    {
        public override string DisplayText => "Validating Secrets...";

        public string Environment { get; set; } = "production";

        public bool Interactive { get; set; }

        public bool Force { get; set; }

        // Keep track of secrets we've seen so we don't validate them twice.
        public HashSet<string> Secrets { get; } = new HashSet<string>();

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            var bindings = service.Outputs.OfType<ComputedBindings>().FirstOrDefault();
            if (bindings is null)
            {
                return;
            }

            foreach (var binding in bindings.Bindings)
            {
                if (!(binding is SecretInputBinding secretInputBinding))
                {
                    continue;
                }

                if (!Secrets.Add(secretInputBinding.Name))
                {
                    output.WriteDebugLine($"Already validated secret '{secretInputBinding.Name}'.");
                    continue;
                }

                output.WriteDebugLine($"Validating secret '{secretInputBinding.Name}'.");

                var config = KubernetesClientConfiguration.BuildDefaultConfig();

                if (!string.IsNullOrEmpty(application.Namespace))
                {
                    config.Namespace = application.Namespace;
                }

                // If namespace is null, set it to default
                config.Namespace ??= "default";

                var kubernetes = new Kubernetes(config);

                try
                {
                    var result = await kubernetes.ReadNamespacedSecretWithHttpMessagesAsync(secretInputBinding.Name, config.Namespace);
                    output.WriteInfoLine($"Found existing secret '{secretInputBinding.Name}'.");
                    continue;
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

                if (Force)
                {
                    output.WriteDebugLine("Skipping because force was specified.");
                    continue;
                }

                if (!Interactive && secretInputBinding is SecretConnectionStringInputBinding)
                {
                    throw new CommandException(
                        $"The secret '{secretInputBinding.Name}' used for service '{secretInputBinding.Service.Name}' is missing from the deployment environment. " +
                        $"Rerun the command with --interactive to specify the value interactively, or with --force to skip validation. Alternatively " +
                        $"use the following command to manually create the secret." + System.Environment.NewLine +
                        $"kubectl create secret generic {secretInputBinding.Name} --namespace {config.Namespace} --from-literal=connectionstring=<value>");
                }

                if (!Interactive && secretInputBinding is SecretUrlInputBinding)
                {
                    throw new CommandException(
                        $"The secret '{secretInputBinding.Name}' used for service '{secretInputBinding.Service.Name}' is missing from the deployment environment. " +
                        $"Rerun the command with --interactive to specify the value interactively, or with --force to skip validation. Alternatively " +
                        $"use the following command to manually create the secret." + System.Environment.NewLine +
                        $"kubectl create secret generic {secretInputBinding.Name} --namespace {config.Namespace} --from-literal=protocol=<value> --from-literal=host=<value> --from-literal=port=<value>");
                }

                V1Secret secret;
                if (secretInputBinding is SecretConnectionStringInputBinding)
                {
                    // If we get here then we should create the secret.
                    var text = output.Prompt($"Enter the connection string to use for service '{secretInputBinding.Service.Name}'", allowEmpty: true);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        output.WriteAlwaysLine($"Skipping creation of secret for '{secretInputBinding.Service.Name}'. This may prevent creation of pods until secrets are created.");
                        output.WriteAlwaysLine($"Manually create a secret with:");
                        output.WriteAlwaysLine($"kubectl create secret generic {secretInputBinding.Name} --namespace {config.Namespace} --from-literal=connectionstring=<value>");
                        continue;
                    }

                    secret = new V1Secret(type: "Opaque", stringData: new Dictionary<string, string>()
                    {
                        { "connectionstring", text },
                    });
                }
                else if (secretInputBinding is SecretUrlInputBinding)
                {
                    // If we get here then we should create the secret.
                    string text;
                    Uri? uri = null;
                    while (true)
                    {
                        text = output.Prompt($"Enter the URI to use for service '{secretInputBinding.Service.Name}'", allowEmpty: true);
                        if (string.IsNullOrEmpty(text))
                        {
                            break; // skip
                        }
                        else if (Uri.TryCreate(text, UriKind.Absolute, out uri))
                        {
                            break; // success
                        }

                        output.WriteAlwaysLine($"Invalid URI: '{text}'");
                    }

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        output.WriteAlwaysLine($"Skipping creation of secret for '{secretInputBinding.Service.Name}'. This may prevent creation of pods until secrets are created.");
                        output.WriteAlwaysLine($"Manually create a secret with:");
                        output.WriteAlwaysLine($"kubectl create secret generic {secretInputBinding.Name} --namespace {config.Namespace} --from-literal=protocol=<value> --from-literal=host=<value> --from-literal=port=<value>");
                        continue;
                    }

                    secret = new V1Secret(type: "Opaque", stringData: new Dictionary<string, string>()
                    {
                        { "protocol", uri!.Scheme },
                        { "host", uri!.Host },
                        { "port", uri!.Port.ToString(CultureInfo.InvariantCulture) },
                    });
                }
                else
                {
                    throw new InvalidOperationException("Unknown Secret type: " + secretInputBinding);
                }

                secret.Metadata = new V1ObjectMeta();
                secret.Metadata.Name = secretInputBinding.Name;
                secret.Metadata.Labels = new Dictionary<string, string>()
                {
                    ["app.kubernetes.io/part-of"] = application.Name,
                };

                output.WriteDebugLine($"Creating secret '{secret.Metadata.Name}'.");

                try
                {
                    await kubernetes.CreateNamespacedSecretWithHttpMessagesAsync(secret, config.Namespace);
                    output.WriteInfoLine($"Created secret '{secret.Metadata.Name}'.");
                }
                catch (Exception ex)
                {
                    output.WriteDebugLine("Failed to create secret.");
                    output.WriteDebugLine(ex.ToString());
                    throw new CommandException("Failed to create secret.", ex);
                }
            }
        }
    }
}



