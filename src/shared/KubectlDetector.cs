// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal static class KubectlDetector
    {
        public static void Initialize(CancellationToken cancellationToken)
        {
            IsKubectlInstalled = new Lazy<Task<bool>>(() => DetectKubectlInstalled(cancellationToken));
            IsKubectlConnectedToCluster = new Lazy<Task<bool>>(() => DetectKubectlConnectedToCluster(cancellationToken));
        }

        public static Lazy<Task<bool>> IsKubectlInstalled { get; private set; } = null!;

        public static Lazy<Task<bool>> IsKubectlConnectedToCluster { get; private set; } = null!;

        private static async Task<bool> DetectKubectlInstalled(CancellationToken cancellationToken)
        {
            try
            {
                // Ignoring the exit code and relying on Process to throw if kubectl is not found
                // kubectl version will return non-zero if you're not connected to a cluster.
                await ProcessUtil.RunAsync("kubectl", "version", throwOnError: false, cancellationToken: cancellationToken);
                return true;
            }
            catch (Exception)
            {
                // Unfortunately, process throws
                return false;
            }
        }

        private static async Task<bool> DetectKubectlConnectedToCluster(CancellationToken cancellationToken)
        {
            try
            {
                var result = await ProcessUtil.RunAsync("kubectl", "cluster-info", throwOnError: false, cancellationToken: cancellationToken);
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
