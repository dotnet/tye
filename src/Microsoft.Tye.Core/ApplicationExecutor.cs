// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public sealed class ApplicationExecutor
    {
        private readonly OutputContext output;

        public ApplicationExecutor(OutputContext output)
        {
            this.output = output;
        }

        public List<ApplicationStep> ApplicationSteps = new List<ApplicationStep>();

        public List<ServiceStep> ServiceSteps { get; } = new List<ServiceStep>();

        public async Task ExecuteAsync(ApplicationBuilder application)
        {
            foreach (var service in application.Services)
            {
                using var tracker = output.BeginStep($"Processing Service '{service.Name}'...");
                foreach (var step in ServiceSteps)
                {
                    using var stepTracker = output.BeginStep(step.DisplayText);
                    await step.ExecuteAsync(output, application, service);
                    stepTracker.MarkComplete();
                }
                tracker.MarkComplete();
            }

            {
                foreach (var step in ApplicationSteps)
                {
                    using var stepTracker = output.BeginStep(step.DisplayText);
                    await step.ExecuteAsync(output, application);
                    stepTracker.MarkComplete();
                }
            }
        }

        public abstract class ApplicationStep
        {
            public abstract string DisplayText { get; }

            public abstract Task ExecuteAsync(OutputContext output, ApplicationBuilder application);
        }

        public abstract class ServiceStep
        {
            public abstract string DisplayText { get; }

            public abstract Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service);

            protected bool SkipWithoutProject(OutputContext output, ServiceBuilder service, [MaybeNullWhen(returnValue: true)] out ProjectServiceBuilder project)
            {
                if (output is null)
                {
                    throw new ArgumentNullException(nameof(output));
                }

                if (service is null)
                {
                    throw new ArgumentNullException(nameof(service));
                }

                if (service is ProjectServiceBuilder p)
                {
                    project = p;
                    return false;
                }

                output.WriteInfoLine($"Service '{service.Name}' does not have a project associated. Skipping.");
                project = default!;
                return true;
            }

            protected bool SkipWithoutContainerInfo(OutputContext output, ServiceBuilder service, [MaybeNullWhen(returnValue: true)] out ContainerInfo container)
            {
                if (output is null)
                {
                    throw new ArgumentNullException(nameof(output));
                }

                if (service is null)
                {
                    throw new ArgumentNullException(nameof(service));
                }

                if (service is ProjectServiceBuilder project && project.ContainerInfo is ContainerInfo c)
                {
                    container = c;
                    return false;
                }

                output.WriteInfoLine($"Service '{service.Name}' does not produce a container. Skipping.");
                container = default!;
                return true;
            }

            protected bool SkipWithoutContainerOutput(OutputContext output, ServiceBuilder service)
            {
                if (output is null)
                {
                    throw new ArgumentNullException(nameof(output));
                }

                if (service is null)
                {
                    throw new ArgumentNullException(nameof(service));
                }

                if (service.Outputs.OfType<DockerImageOutput>().Any())
                {
                    return false;
                }

                output.WriteInfoLine($"Service '{service.Name}' does not have a container. Skipping.");
                return true;
            }
        }
    }
}
