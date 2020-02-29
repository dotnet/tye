using System.IO;

namespace Opulence
{
    internal static class Templates
    {
        public static string RootDirectoryPath
        {
            get
            {
                return Path.Combine(
                    Path.GetDirectoryName(typeof(HelmChartGenerator).Assembly.Location)!,
                    "Templates");
            }
        }

        public static string HelmChartsDirectoryPath
        {
            get
            {
                return Path.Combine(RootDirectoryPath, "Helm");
            }
        }
    }
}
