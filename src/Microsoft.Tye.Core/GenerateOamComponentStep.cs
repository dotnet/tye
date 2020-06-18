﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal sealed class GenerateOamComponentStep : ApplicationExecutor.ServiceStep
    {
        public override string DisplayText => "Generating Manifests...";

        public string Environment { get; set; } = "production";


        public override Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            if (SkipWithoutContainerOutput(output, service))
            {
                return Task.CompletedTask;
            }

            if (SkipWithoutDotnetProject(output, service, out var project))
            {
                return Task.CompletedTask;
            }

            var component = OamComponentGenerator.CreateOamComponent(output, application, project);
            service.Outputs.Add(component);
            return Task.CompletedTask;
        }
    }
}
