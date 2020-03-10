// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Tye.ConfigModel;

namespace Tye
{
    public static class BuildHost
    {
        public static Task BuildAsync(IConsole console, FileInfo path, Verbosity verbosity, bool interactive)
        {
            var application = ConfigFactory.FromFile(path);
            return ExecuteBuildAsync(new OutputContext(console, verbosity), application, environment: "production", interactive);
        }

        public static async Task ExecuteBuildAsync(OutputContext output, ConfigApplication application, string environment, bool interactive)
        {
            var temporaryApplication = await Program.CreateApplicationAdapterAsync(output, application, interactive, requireRegistry: false);
            var steps = new List<ServiceExecutor.Step>()
            {
                new CombineStep() { Environment = environment, },
                new PublishProjectStep(),
                new BuildDockerImageStep() { Environment = environment, },
            };

            var executor = new ServiceExecutor(output, temporaryApplication, steps);
            foreach (var service in temporaryApplication.Services)
            {
                await executor.ExecuteAsync(service);
            }
        }
    }
}
