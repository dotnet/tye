// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public static class GenerateHost
    {
        public static async Task GenerateAsync(OutputContext output, FileInfo path, bool interactive, string @namespace, string? framework = null, ApplicationFactoryFilter? filter = null)
        {
            var application = await ApplicationFactory.CreateAsync(output, path, framework, filter);

            if (application.Services.Count == 0)
            {
                throw new CommandException($"No services found in \"{application.Source.Name}\"");
            }
            if (!string.IsNullOrEmpty(@namespace))
            {
                application.Namespace = @namespace;
            }
            await ExecuteGenerateAsync(output, application, environment: "production", interactive);
        }

        public static async Task ExecuteGenerateAsync(OutputContext output, ApplicationBuilder application, string environment, bool interactive)
        {
            await application.ProcessExtensionsAsync(options: null, output, ExtensionContext.OperationKind.Deploy);
            Program.ApplyRegistry(output, application, interactive, requireRegistry: false);

            var executor = new ApplicationExecutor(output)
            {
                ServiceSteps =
                {
                    new ApplyContainerDefaultsStep(),
                    new CombineStep() { Environment = environment, },
                    new PublishProjectStep(),
                    new BuildDockerImageStep() { Environment = environment, }, // Make an image but don't push it
                    new GenerateServiceKubernetesManifestStep() { Environment = environment, },
                },

                IngressSteps =
                {
                    new GenerateIngressKubernetesManifestStep(),
                },

                ApplicationSteps =
                {
                    new GenerateApplicationKubernetesManifestStep() { Environment = environment, },
                }
            };

            await executor.ExecuteAsync(application);
        }
    }
}
