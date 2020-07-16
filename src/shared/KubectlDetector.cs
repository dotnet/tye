// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal class KubectlDetector
    {
        private static bool? _kubectlInstalled = null;
        private static bool? _kubectlConnectedToCluster = null;

        public static async Task<bool> IsKubectlInstalledAsync(OutputContext output)
        {
            if (!_kubectlInstalled.HasValue)
            {
                output.WriteInfoLine("Verifying kubectl installation...");
                try
                {
                    // Ignoring the exit code and relying on Process to throw if kubectl is not found
                    // kubectl version will return non-zero if you're not connected to a cluster.
                    await ProcessUtil.RunAsync("kubectl", "version", throwOnError: false);
                    _kubectlInstalled = true;
                }
                catch (Exception)
                {
                    // Unfortunately, process throws
                    _kubectlInstalled = false;
                }
            }

            return _kubectlInstalled.Value;
        }

        public static async Task<bool> IsKubectlConnectedToClusterAsync(OutputContext output)
        {
            if (!_kubectlConnectedToCluster.HasValue)
            {
                output.WriteInfoLine("Verifying kubectl connection to cluster...");
                try
                {
                    var result = await ProcessUtil.RunAsync("kubectl", "cluster-info", throwOnError: false);
                    _kubectlConnectedToCluster = result.ExitCode == 0;
                }
                catch (Exception)
                {
                    // Unfortunately, process throws
                    _kubectlConnectedToCluster = false;
                }
            }
            return _kubectlConnectedToCluster.Value;
        }
    }
}
