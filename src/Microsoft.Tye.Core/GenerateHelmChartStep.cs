﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal sealed class GenerateHelmChartStep : ApplicationExecutor.ServiceStep
    {
        public override string DisplayText => "Generating Helm Chart...";

        public bool Force { get; set; }

        public override Task ExecuteAsync(OutputContext output, ApplicationBuilder application, ServiceBuilder service)
        {
            if (SkipWithoutDotnetProject(output, service, out var project))
            {
                return Task.CompletedTask;
            }

            if (SkipWithoutContainerInfo(output, service, out var container))
            {
                return Task.CompletedTask;
            }

            var chartDirectory = Path.Combine(project.ProjectFile.DirectoryName, "charts");
            if (Directory.Exists(chartDirectory) && !Force)
            {
                throw new CommandException("'charts' directory already exists for project. use '--force' to overwrite.");
            }
            else if (Directory.Exists(chartDirectory))
            {
                Directory.Delete(chartDirectory, recursive: true);
            }

            var chart = new HelmChartStep();
            output.WriteInfoLine($"Generating Helm Chart at '{Path.Combine(chartDirectory, chart.ChartName)}'.");
            return HelmChartGenerator.GenerateAsync(
                output,
                application,
                project,
                container,
                chart,
                new DirectoryInfo(chartDirectory));

        }
    }
}
