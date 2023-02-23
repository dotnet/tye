// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    internal static class KubectlDetector
    {
        private static Lazy<Task<Version?>> _kubectlInstalled = new Lazy<Task<Version?>>(GetKubectlVersion);
        private static Lazy<Task<bool>> _kubectlConnectedToCluster = new Lazy<Task<bool>>(DetectKubectlConnectedToCluster);
        private static string? _namespace = null;

        public static Task<Version?> GetKubernetesServerVersion(OutputContext output)
        {
            if (!_kubectlInstalled.IsValueCreated)
            {
                output.WriteInfoLine("Verifying kubectl installation...");
            }
            return _kubectlInstalled.Value;
        }

        public static Task<bool> IsKubectlConnectedToClusterAsync(OutputContext output, string? @namespace = null)
        {
            if (!_kubectlConnectedToCluster.IsValueCreated)
            {
                _namespace = @namespace;
                output.WriteInfoLine("Verifying kubectl connection to cluster...");
            }
            return _kubectlConnectedToCluster.Value;
        }

        private static async Task<Version?> GetKubectlVersion()
        {
            try
            {
                // Ignoring the exit code and relying on Process to throw if kubectl is not found
                // kubectl version will return non-zero if you're not connected to a cluster.
                var result = await ProcessUtil.RunAsync("kubectl", "version -o json", throwOnError: false);

                var output = result.StandardOutput;
                using var jsonDoc = JsonDocument.Parse(output);
                foreach (JsonProperty element in jsonDoc.RootElement.EnumerateObject())
                {
                    if (element.Name != "serverVersion")
                    {
                        continue;
                    }
                    var majorString = element.Value.GetProperty("major").GetString();
                    var minorString = element.Value.GetProperty("minor").GetString();

                    if (majorString != null && minorString != null)
                    {
                        var major = int.Parse(majorString);
                        var minor = int.Parse(minorString.Trim('+'));
                        var version = new Version(major, minor);
                        return version;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                // Unfortunately, process throws
                return null;
            }
        }

        private static async Task<bool> DetectKubectlConnectedToCluster()
        {
            try
            {
                String args = "cluster-info";
                if (_namespace != null) {
                    args += $" --namespace {_namespace}";
                }

                var result = await ProcessUtil.RunAsync("kubectl", args, throwOnError: false);
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
