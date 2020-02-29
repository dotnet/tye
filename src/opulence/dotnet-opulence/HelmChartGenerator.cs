using System;
using System.IO;
using System.Threading.Tasks;

namespace Opulence
{
    internal static class HelmChartGenerator
    {
        public static async Task GenerateAsync(OutputContext output, Application application, ServiceEntry service, Project project, ContainerInfo container, HelmChartStep chart, DirectoryInfo outputDirectory)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (service is null)
            {
                throw new ArgumentNullException(nameof(service));
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

            if (outputDirectory is null)
            {
                throw new ArgumentNullException(nameof(outputDirectory));
            }

            ApplyHelmChartDefaults(application, service, container, chart);

            // The directory with the charts needs to be the same as the chart name
            var chartDirectoryPath = Path.Combine(outputDirectory.FullName, chart.ChartName);
            Directory.CreateDirectory(chartDirectoryPath);

            var templateDirectoryPath = Path.Combine(
                Path.GetDirectoryName(typeof(HelmChartGenerator).Assembly.Location)!,
                "Templates",
                "Helm");

            DirectoryCopy.Copy(templateDirectoryPath, chartDirectoryPath);

            // Write Chart.yaml
            //
            // apiVersion: v1
            // name: <appname>
            // version: <version>
            // appVersion: <version>
            await File.WriteAllLinesAsync(Path.Combine(chartDirectoryPath, "Chart.yaml"), new[]
            {
                $"apiVersion: v1",
                $"name: {chart.ChartName}",
                $"# helm requires the version and appVersion to specified in Chart.yaml",
                $"# opulence will override these values when packaging the chart",
                $"version: {project.Version.Replace('+', '-')}",
                $"appVersion: {project.Version.Replace('+', '-')}"
            });

            // Write values.yaml
            //
            // image:
            //   repository: rynowak.azurecr.io/rochambot/gamemaster
            await File.WriteAllLinesAsync(Path.Combine(chartDirectoryPath, "values.yaml"), new[]
            {
                $"image:",
                $"  repository: {container.ImageName}",
            });
        }

        public static void ApplyHelmChartDefaults(Application application, ServiceEntry service, ContainerInfo container, HelmChartStep chart)
        {
            if (application is null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (service is null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (container is null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (chart is null)
            {
                throw new ArgumentNullException(nameof(chart));
            }

            chart.ChartName ??= service.Service.Name.ToLowerInvariant();
        }
    }
}