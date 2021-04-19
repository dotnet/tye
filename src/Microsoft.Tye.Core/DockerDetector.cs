// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public class DockerDetector
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public static DockerDetector Instance { get; } = new DockerDetector();

        private bool _isUsable { get; }
        private string? _unusableReason { get; }

        public bool IsPodman { get; }
        public string AspNetUrlsHost { get; }
        public string? ContainerHost { get; }

        public bool IsUsable(out string? unusableReason)
        {
            unusableReason = _unusableReason;
            return _isUsable;
        }

        private DockerDetector()
        {
            AspNetUrlsHost = "localhost";
            try
            {
                ProcessResult result;
                try
                {
                    // try to use podman.
                    result = ProcessUtil.RunAsync("podman", "version -f \"{{ .Client.Version }}\"", throwOnError: false, cancellationToken: new CancellationTokenSource(Timeout).Token).Result;
                    IsPodman = true;

                    if (result.ExitCode != 0)
                    {
                        _unusableReason = $"podman version exited with {result.ExitCode}. Standard error: \"{result.StandardError}\".";
                        return;
                    }

                    if (!Version.TryParse(result.StandardOutput, out Version? version))
                    {
                        _unusableReason = $"cannot parse podman version '{result.StandardOutput}'.";
                        return;
                    }
                    Version minVersion = new Version(3, 1);
                    if (version < minVersion)
                    {
                        _unusableReason = $"podman version '{result.StandardOutput}' is less than the required '{minVersion}'.";
                        return;
                    }

                    // Check if podman is configured to allow containers to access host services.
                    bool hostLoopbackEnabled = false;
                    string containersConfPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                                                             "containers/containers.conf");
                    string[] containersConf = File.Exists(containersConfPath) ? File.ReadAllLines(containersConfPath) : Array.Empty<string>();
                    // Poor man's TOML parsing.
                    foreach (var line in containersConf)
                    {
                        string trimmed = line.Replace(" ", "");
                        if (trimmed.StartsWith("network_cmd_options=", StringComparison.InvariantCultureIgnoreCase) &&
                            trimmed.Contains("\"allow_host_loopback=true\""))
                        {
                            hostLoopbackEnabled = true;
                            break;
                        }
                    }
                    if (hostLoopbackEnabled)
                    {
                        ContainerHost = "10.0.2.2";
                    }
                }
                catch (Exception)
                {
                    // try to use docker.

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        // See: https://github.com/docker/for-linux/issues/264
                        //
                        // host.docker.internal is making it's way into linux docker but doesn't work yet
                        // instead we use the machine IP
                        var addresses = Dns.GetHostAddresses(Dns.GetHostName());
                        ContainerHost = addresses[0].ToString();

                        // We need to bind to all interfaces on linux since the container -> host communication won't work
                        // if we use the IP address to reach out of the host. This works fine on osx and windows
                        // but doesn't work on linux.
                        AspNetUrlsHost = "*";
                    }
                    else
                    {
                        ContainerHost = "host.docker.internal";
                    }

                    result = ProcessUtil.RunAsync("docker", "version", throwOnError: false, cancellationToken: new CancellationTokenSource(Timeout).Token).Result;

                    if (result.ExitCode != 0)
                    {
                        _unusableReason = "docker is not connected.";
                        return;
                    }
                }

                _isUsable = true;
            }
            catch (Exception)
            {
                _unusableReason = "docker is not installed.";
            }
        }
    }
}
