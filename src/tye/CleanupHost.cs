// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Rest;
using Microsoft.Tye.ConfigModel;
using Newtonsoft.Json;

namespace Microsoft.Tye
{
    public static class CleanupHost
    {
        public static async Task CleanupAsync(IConsole console, FileInfo path, Verbosity verbosity, bool interactive, bool whatIf)
        {
            var output = new OutputContext(console, verbosity);

            output.WriteInfoLine("Loading Application Details...");

            // We don't need to know anything about the services, just the application name.
            var application = ConfigFactory.FromFile(path);

            await ExecuteCleanupAsync(output, application, interactive, whatIf);
        }

        public static async Task ExecuteCleanupAsync(OutputContext output, ConfigApplication application, bool interactive, bool whatIf)
        {
            var config = KubernetesClientConfiguration.BuildDefaultConfig();

            // Workaround for https://github.com/kubernetes-client/csharp/issues/372
            var store = await KubernetesClientConfiguration.LoadKubeConfigAsync();
            var context = store.Contexts.Where(c => c.Name == config.CurrentContext).FirstOrDefault();
            config.Namespace ??= context?.ContextDetails?.Namespace;

            var kubernetes = new Kubernetes(config);

            // Due to some limitations in the k8s SDK we currently have a hardcoded list of resource
            // types that we handle deletes for. If we start adding extensibility for the *kinds* of
            // k8s resources we create, or the ability to deploy additional files along with the
            // resources we understand then we should revisit this.
            //
            // Basically the challenges are:
            //
            // - kubectl api-resources --all (and similar) are implemented client-side (n+1 problem)
            // - the C# k8s SDK doesn't have an untyped api for operations on arbitrary resources, the
            //   closest thing is the custom resource APIs
            // - Legacy resources without an api group don't follow the same URL scheme as more modern 
            //   ones, and thus cannot be addressed using the custom resource APIs.
            //
            // So solving 'cleanup' generically would involve doing a bunch of work to query things 
            // generically, including going outside of what's provided by the SDK.
            //
            // - querying api-resources
            // - querying api-groups
            // - handcrafing requests to list for each resource
            // - handcrafting requests to delete each resource
            var resources = new List<Resource>();

            try
            {
                output.WriteDebugLine("Querying services");
                var response = await kubernetes.ListNamespacedServiceWithHttpMessagesAsync(
                    config.Namespace,
                    labelSelector: $"app.kubernetes.io/part-of={application.Name}");

                foreach (var resource in response.Body.Items)
                {
                    resource.Kind = V1Service.KubeKind;
                }

                resources.AddRange(response.Body.Items.Select(item => new Resource(item, item.Metadata, DeleteService)));
                output.WriteDebugLine($"Found {response.Body.Items.Count} matching services");
            }
            catch (Exception ex)
            {
                output.WriteDebugLine("Failed to query services.");
                output.WriteDebugLine(ex.ToString());
                throw new CommandException("Unable connect to kubernetes.", ex);
            }

            try
            {
                output.WriteDebugLine("Querying deployments");
                var response = await kubernetes.ListNamespacedDeploymentWithHttpMessagesAsync(
                    config.Namespace,
                    labelSelector: $"app.kubernetes.io/part-of={application.Name}");

                foreach (var resource in response.Body.Items)
                {
                    resource.Kind = V1Deployment.KubeKind;
                }

                resources.AddRange(response.Body.Items.Select(item => new Resource(item, item.Metadata, DeleteDeployment)));
                output.WriteDebugLine($"Found {response.Body.Items.Count} matching deployments");
            }
            catch (Exception ex)
            {
                output.WriteDebugLine("Failed to query deployments.");
                output.WriteDebugLine(ex.ToString());
                throw new CommandException("Unable connect to kubernetes.", ex);
            }

            try
            {
                output.WriteDebugLine("Querying secrets");
                var response = await kubernetes.ListNamespacedSecretWithHttpMessagesAsync(
                    config.Namespace,
                    labelSelector: $"app.kubernetes.io/part-of={application.Name}");

                foreach (var resource in response.Body.Items)
                {
                    resource.Kind = V1Secret.KubeKind;
                }

                resources.AddRange(response.Body.Items.Select(item => new Resource(item, item.Metadata, DeleteSecret)));
                output.WriteDebugLine($"Found {response.Body.Items.Count} matching secrets");

            }
            catch (Exception ex)
            {
                output.WriteDebugLine("Failed to query secrets.");
                output.WriteDebugLine(ex.ToString());
                throw new CommandException("Unable connect to kubernetes.", ex);
            }

            output.WriteInfoLine($"Found {resources.Count} resource(s).");

            var exceptions = new List<(Resource resource, HttpOperationException exception)>();
            foreach (var resource in resources)
            {
                var operation = Operations.Delete;
                if (interactive && !output.Confirm($"Delete {resource.Obj.Kind} '{resource.Metadata.Name}'?"))
                {
                    operation = Operations.None;
                }

                if (whatIf && operation == Operations.Delete)
                {
                    operation = Operations.Explain;
                }

                if (operation == Operations.None)
                {
                    output.WriteAlwaysLine($"Skipping '{resource.Obj.Kind}' '{resource.Metadata.Name}' ...");
                }
                else if (operation == Operations.Explain)
                {
                    output.WriteAlwaysLine($"whatif: Deleting '{resource.Obj.Kind}' '{resource.Metadata.Name}' ...");
                }
                else if (operation == Operations.Delete)
                {
                    output.WriteAlwaysLine($"Deleting '{resource.Obj.Kind}' '{resource.Metadata.Name}' ...");

                    try
                    {
                        var response = await resource.Deleter(resource.Metadata.Name);

                        output.WriteDebugLine($"Successfully deleted resource: '{resource.Obj.Kind}' '{resource.Metadata.Name}'.");
                    }
                    catch (HttpOperationException ex)
                    {
                        output.WriteDebugLine($"Failed to delete resource: '{resource.Obj.Kind}' '{resource.Metadata.Name}'.");
                        output.WriteDebugLine(ex.ToString());
                        exceptions.Add((resource, ex));
                    }
                }
            }

            if (exceptions.Count > 0)
            {
                throw new CommandException(
                    $"Failed to delete some resources: " + Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, exceptions.Select(e => $"\t'{e.resource.Obj.Kind}' '{e.resource.Metadata.Name}': {e.exception.Body}.")));
            }

            Task<Rest.HttpOperationResponse<V1Status>> DeleteService(string name)
            {
                return kubernetes!.DeleteNamespacedServiceWithHttpMessagesAsync(name, config!.Namespace);
            }

            Task<Rest.HttpOperationResponse<V1Status>> DeleteDeployment(string name)
            {
                return kubernetes!.DeleteNamespacedDeploymentWithHttpMessagesAsync(name, config!.Namespace);
            }

            Task<Rest.HttpOperationResponse<V1Status>> DeleteSecret(string name)
            {
                return kubernetes!.DeleteNamespacedSecretWithHttpMessagesAsync(name, config!.Namespace);
            }
        }

        private enum Operations
        {
            None,
            Delete,
            Explain,
        }

        private readonly struct Resource
        {
            public readonly IKubernetesObject Obj;
            public readonly V1ObjectMeta Metadata;
            public readonly Func<string, Task<Rest.HttpOperationResponse<V1Status>>> Deleter;

            public Resource(IKubernetesObject obj, V1ObjectMeta metadata, Func<string, Task<Rest.HttpOperationResponse<V1Status>>> deleter)
            {
                Obj = obj;
                Metadata = metadata;
                Deleter = deleter;
            }
        }
    }
}
