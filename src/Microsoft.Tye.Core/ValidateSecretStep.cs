// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Rest;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    public sealed class ValidateSecretStep : ServiceExecutor.Step
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
                if (binding is SecretInputBinding secretInputBinding)
                {
                    if (!Secrets.Add(secretInputBinding.Name))
                    {
                        output.WriteDebugLine($"Already validated secret '{secretInputBinding.Name}'.");
                        continue;
                    }

                    output.WriteDebugLine($"Validating secret '{secretInputBinding.Name}'.");

                    var config = KubernetesClientConfiguration.BuildDefaultConfig();

                    // Workaround for https://github.com/kubernetes-client/csharp/issues/372
                    var store = KubernetesClientConfiguration.LoadKubeConfig();
                    var context = store.Contexts.Where(c => c.Name == config.CurrentContext).FirstOrDefault();
                    config.Namespace ??= context?.ContextDetails?.Namespace;

                    var kubernetes = new Kubernetes(config);

                    try
                    {
                        var result = await kubernetes.ReadNamespacedSecretWithHttpMessagesAsync(secretInputBinding.Name, config.Namespace ?? "default");
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

                    if (!Interactive)
                    {
                        throw new CommandException(
                            $"The secret '{secretInputBinding.Name}' used for service '{secretInputBinding.Service.Name}' is missing from the deployment environment. " +
                            $"Rerun the command with --interactive to specify the value interactively, or with --force to skip validation. Alternatively " +
                            $"use the following command to manually create the secret." + System.Environment.NewLine +
                            $"kubectl create secret generic {secretInputBinding.Name} --from-literal=connectionstring=<value>");
                    }

                    // If we get here then we should create the sceret.
                    var text = output.Prompt($"Enter the connection string to use for service '{secretInputBinding.Service.Name}'", allowEmpty: true);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        output.WriteAlways($"Skipping creation of secret for '{secretInputBinding.Service.Name}'. This may prevent creation of pods until secrets are created.");
                        output.WriteAlways($"Manually create a secret with:");
                        output.WriteAlways($"kubectl create secret generic {secretInputBinding.Name} --from-literal=connectionstring=<value>");
                        continue;
                    }

                    var secret = new V1Secret(type: "Opaque", stringData: new Dictionary<string, string>()
                    {
                        { "connectionstring", text },
                    });
                    secret.Metadata = new V1ObjectMeta();
                    secret.Metadata.Name = secretInputBinding.Name;

                    output.WriteDebugLine($"Creating secret '{secret.Metadata.Name}'.");

                    try
                    {
                        await kubernetes.CreateNamespacedSecretWithHttpMessagesAsync(secret, config.Namespace ?? "default");
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

            var yaml = service.Outputs.OfType<IYamlManifestOutput>().ToArray();
            if (yaml.Length == 0)
            {
                output.WriteDebugLine($"No yaml manifests found for service '{service.Name}'. Skipping.");
                return;
            }

            using var tempFile = TempFile.Create();
            output.WriteDebugLine($"Writing output to '{tempFile.FilePath}'.");

            {
                using var stream = File.OpenWrite(tempFile.FilePath);
                using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: -1, leaveOpen: true);
                var yamlStream = new YamlStream(yaml.Select(y => y.Yaml));
                yamlStream.Save(writer, assignAnchors: false);
            }

            // kubectl apply logic is implemented in the client in older versions of k8s. The capability
            // to get the same behavior in the server isn't present in every version that's relevant.
            //
            // https://kubernetes.io/docs/reference/using-api/api-concepts/#server-side-apply
            //
            output.WriteDebugLine("Running 'kubectl apply'.");
            output.WriteCommandLine("kubectl", $"apply -f \"{tempFile.FilePath}\"");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
                $"kubectl",
                $"apply -f \"{tempFile.FilePath}\"",
                System.Environment.CurrentDirectory,
                stdOut: capture.StdOut,
                stdErr: capture.StdErr);

            output.WriteDebugLine($"Done running 'kubectl apply' exit code: {exitCode}");
            if (exitCode != 0)
            {
                throw new CommandException("'kubectl apply' failed.");
            }

            output.WriteInfoLine($"Deployed service '{service.Name}'.");
        }
    }
}



