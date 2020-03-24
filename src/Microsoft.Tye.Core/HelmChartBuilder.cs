// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal static class HelmChartBuilder
    {
        public static async Task BuildHelmChartAsync(OutputContext output, ApplicationBuilder application, ProjectServiceBuilder project, ContainerInfo container, HelmChartStep chart)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (container is null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (chart is null)
            {
                throw new ArgumentNullException(nameof(chart));
            }

            var projectDirectory = project.ProjectFile.DirectoryName;
            var outputDirectoryPath = Path.Combine(projectDirectory, "bin");
            using var tempDirectory = TempDirectory.Create();

            HelmChartGenerator.ApplyHelmChartDefaults(application, project, container, chart);

            var chartRoot = Path.Combine(projectDirectory, "charts");
            var chartPath = Path.Combine(chartRoot, chart.ChartName);

            output.WriteDebugLine($"Looking for existing chart in '{chartPath}'.");
            if (Directory.Exists(chartPath))
            {
                output.WriteDebugLine($"Found existing chart in '{chartPath}'.");
            }
            else
            {
                chartRoot = tempDirectory.DirectoryPath;
                chartPath = Path.Combine(chartRoot, chart.ChartName);
                output.WriteDebugLine($"Generating chart in '{chartPath}'.");
                await HelmChartGenerator.GenerateAsync(output, application, project, container, chart, new DirectoryInfo(tempDirectory.DirectoryPath));
            }

            output.WriteDebugLine("Running 'helm package'.");
            output.WriteCommandLine("helm", $"package -d \"{outputDirectoryPath}\" --version {project.Version.Replace('+', '-')} --app-version {project.Version.Replace('+', '-')}");
            var capture = output.Capture();
            var exitCode = await Process.ExecuteAsync(
                "helm",
                $"package . -d \"{outputDirectoryPath}\" --version {project.Version.Replace('+', '-')} --app-version {project.Version.Replace('+', '-')}",
                workingDir: chartPath,
                stdOut: capture.StdOut,
                stdErr: capture.StdErr);

            output.WriteDebugLine($"Running 'helm package' exit code: {exitCode}");
            if (exitCode != 0)
            {
                throw new CommandException("Running 'helm package' failed.");
            }

            output.WriteInfoLine($"Created Helm Chart: {Path.Combine(outputDirectoryPath, chart.ChartName + "-" + project.Version.Replace('+', '-') + ".tgz")}");
        }
    }
}
