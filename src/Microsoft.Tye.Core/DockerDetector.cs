// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public class DockerDetector
    {
        public static DockerDetector Instance { get; } = new DockerDetector();

        private DockerDetector()
        {
            IsDockerInstalled = new Lazy<Task<bool>>(DetectDockerInstalled);
            IsDockerConnectedToDaemon = new Lazy<Task<bool>>(DetectDockerConnectedToDaemon);
        }

        public Lazy<Task<bool>> IsDockerInstalled { get; }

        public Lazy<Task<bool>> IsDockerConnectedToDaemon { get; }

        private async Task<bool> DetectDockerInstalled()
        {
            try
            {
                await ProcessUtil.RunAsync("docker", "version", throwOnError: false);
                return true;
            }
            catch (Exception)
            {
                // Unfortunately, process throws
                return false;
            }
        }

        private async Task<bool> DetectDockerConnectedToDaemon()
        {
            try
            {
                var result = await ProcessUtil.RunAsync("docker", "version", throwOnError: false);
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
