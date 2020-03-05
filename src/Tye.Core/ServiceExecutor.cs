// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Tye
{
    public sealed class ServiceExecutor
    {
        private readonly OutputContext output;
        private readonly Application application;
        private readonly Step[] steps;

        public ServiceExecutor(OutputContext output, Application application, IEnumerable<Step> steps)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (steps is null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            this.output = output;
            this.application = application;
            this.steps = steps.ToArray();
        }

        public async Task ExecuteAsync(ServiceEntry service)
        {
            using var tracker = output.BeginStep($"Processing Service '{service.FriendlyName}'...");
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];

                using var stepTracker = output.BeginStep(step.DisplayText);
                await step.ExecuteAsync(output, application, service);
                stepTracker.MarkComplete();
            }
            tracker.MarkComplete();
        }

        public abstract class Step
        {
            public abstract string DisplayText { get; }

            public abstract Task ExecuteAsync(OutputContext output, Application application, ServiceEntry service);

            protected bool SkipForEnvironment(OutputContext output, ServiceEntry service, string environment)
            {
                if (!service.AppliesToEnvironment(environment))
                {
                    output.WriteDebugLine($"Service '{service.FriendlyName}' is not part of environment '{environment}'. Skipping.");
                    return true;
                }

                return false;
            }

            protected bool SkipWithoutProject(OutputContext output, ServiceEntry service, [MaybeNullWhen(returnValue: true)] out Project project)
            {
                if (output is null)
                {
                    throw new ArgumentNullException(nameof(output));
                }

                if (service is null)
                {
                    throw new ArgumentNullException(nameof(service));
                }

                if (service.Service.Source is Project p)
                {
                    project = p;
                    return false;
                }

                output.WriteInfoLine($"Service '{service.FriendlyName}' does not have a project associated. Skipping.");
                project = default!;
                return true;
            }

            protected bool SkipWithoutContainerInfo(OutputContext output, ServiceEntry service, [MaybeNullWhen(returnValue: true)] out ContainerInfo container)
            {
                if (output is null)
                {
                    throw new ArgumentNullException(nameof(output));
                }

                if (service is null)
                {
                    throw new ArgumentNullException(nameof(service));
                }

                if (service.Service.GeneratedAssets.Container is ContainerInfo c)
                {
                    container = c;
                    return false;
                }

                output.WriteInfoLine($"Service '{service.FriendlyName}' does not produce a container. Skipping.");
                container = default!;
                return true;
            }

            protected bool SkipWithoutContainerOutput(OutputContext output, ServiceEntry service)
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

                output.WriteInfoLine($"Service '{service.FriendlyName}' does not have a container. Skipping.");
                return true;
            }
        }
    }
}
