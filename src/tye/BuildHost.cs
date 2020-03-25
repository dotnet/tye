// Licensed to the .NET Foundation under one or more agreements.
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
            var steps = new List<ServiceExecutor.Step>()
            {
                new CombineStep() { Environment = environment, },
                new PublishProjectStep(),
                new BuildDockerImageStep() { Environment = environment, },
            };

            Program.ApplyRegistryAndDefaults(output, application, interactive, requireRegistry: false);

            var executor = new ServiceExecutor(output, application, steps);
            foreach (var service in application.Services)
            {
                await executor.ExecuteAsync(service);
            }
        }
    }
}
