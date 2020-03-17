// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Tye
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
