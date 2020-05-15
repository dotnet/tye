﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public static class BuildHost
    {
        public static async Task BuildAsync(IConsole console, FileInfo path, Verbosity verbosity, bool interactive)
        {
            var output = new OutputContext(console, verbosity);
            var application = await ApplicationFactory.CreateAsync(output, path);
            if (application.Services.Count == 0)
            {
                throw new CommandException($"No services found in \"{application.Source.Name}\"");
            }

            await ExecuteBuildAsync(output, application, environment: "production", interactive);
        }

        public static async Task ExecuteBuildAsync(OutputContext output, ApplicationBuilder application, string environment, bool interactive)
        {
            await application.ProcessExtensionsAsync(output, ExtensionContext.OperationKind.Deploy);
            Program.ApplyRegistry(output, application, interactive, requireRegistry: false);

            var executor = new ApplicationExecutor(output)
            {
                ServiceSteps =
                {
                    new ApplyContainerDefaultsStep(),
                    new CombineStep() { Environment = environment, },
                    new PublishProjectStep(),
                    new BuildDockerImageStep() { Environment = environment, },
                },
            };
            await executor.ExecuteAsync(application);
        }
    }
}
