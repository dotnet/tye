// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal static class KubectlDetector
    {
        private static Lazy<Task<bool>> _kubectlInstalled = new Lazy<Task<bool>>(DetectKubectlInstalled);
        private static Lazy<Task<bool>> _kubectlConnectedToCluster = new Lazy<Task<bool>>(DetectKubectlConnectedToCluster);

        public static Task<bool> IsKubectlInstalledAsync(OutputContext output)
        {
            if (!_kubectlInstalled.IsValueCreated)
            {
                output.WriteInfoLine("Verifying kubectl installation...");
            }
            return _kubectlInstalled.Value;
        }

        public static Task<bool> IsKubectlConnectedToClusterAsync(OutputContext output)
        {
            if (!_kubectlConnectedToCluster.IsValueCreated)
            {
                output.WriteInfoLine("Verifying kubectl connection to cluster...");
            }
            return _kubectlConnectedToCluster.Value;
        }

        private static async Task<bool> DetectKubectlInstalled()
        {
            try
            {
                // Ignoring the exit code and relying on Process to throw if kubectl is not found
                // kubectl version will return non-zero if you're not connected to a cluster.
                await ProcessUtil.RunAsync("kubectl", "version", throwOnError: false);
                return true;
            }
            catch (Exception)
            {
                // Unfortunately, process throws
                return false;
            }
        }

        private static async Task<bool> DetectKubectlConnectedToCluster()
        {
            try
            {
                var result = await ProcessUtil.RunAsync("kubectl", "cluster-info", throwOnError: false);
                return result.ExitCode == 0;
            }
            catch (Exception)
            {
                // Unfortunately, process throws
                return false;
            }
        }
    }
}
