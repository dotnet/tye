// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Tye
{
    internal sealed class GenerateOamComponentStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Generating Manifests...";

        public string Environment { get; set; } = "production";


        public override Task ExecuteAsync(OutputContext output, Application application, ServiceEntry service)
        {
            if (SkipWithoutContainerOutput(output, service))
            {
                return Task.CompletedTask;
            }

            if (SkipForEnvironment(output, service, Environment))
            {
                return Task.CompletedTask;
            }

            var component = OamComponentGenerator.CreateOamComponent(output, application, service);
            service.Outputs.Add(component);
            return Task.CompletedTask;
        }
    }
}
