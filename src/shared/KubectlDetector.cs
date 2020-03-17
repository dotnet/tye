// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal class KubectlDetector
    {
        public static KubectlDetector Instance { get; } = new KubectlDetector();

        private KubectlDetector()
        {
            IsKubectlInstalled = new Lazy<Task<bool>>(DetectKubectlInstalled);
            IsKubectlConnectedToCluster = new Lazy<Task<bool>>(DetectKubectlConnectedToCluster);
        }

        public Lazy<Task<bool>> IsKubectlInstalled { get; }

        public Lazy<Task<bool>> IsKubectlConnectedToCluster { get; }

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
