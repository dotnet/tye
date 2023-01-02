// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Tye.ConfigModel;

namespace Microsoft.Tye
{
    public class ContainerEngine
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        // Used by tests:
        public static ContainerEngine? s_default;
        public static ContainerEngine Default
            => (s_default ??= new ContainerEngine(default));

        private bool _isUsable { get; }
        private string? _unusableReason;
        private bool _isPodman;
        private string? _containerHost;
        private string _aspnetUrlsHost;

        public string AspNetUrlsHost => _aspnetUrlsHost;
        public string? ContainerHost => _containerHost;
        public bool IsPodman => _isPodman;

        public Task<int> ExecuteAsync(
            string args,
            string? workingDir = null,
            Action<string>? stdOut = null,
            Action<string>? stdErr = null,
            params (string key, string value)[] environmentVariables)
        => ProcessUtil.ExecuteAsync(CommandName, args, workingDir, stdOut, stdErr, environmentVariables);

        public Task<ProcessResult> RunAsync(
            string arguments,
            string? workingDirectory = null,
            bool throwOnError = true,
            IDictionary<string, string>? environmentVariables = null,
            Action<string>? outputDataReceived = null,
            Action<string>? errorDataReceived = null,
            Action<int>? onStart = null,
            Action<int>? onStop = null,
            CancellationToken cancellationToken = default)
        => ProcessUtil.RunAsync(CommandName, arguments, workingDirectory, throwOnError, environmentVariables,
            outputDataReceived, errorDataReceived, onStart, onStop, cancellationToken);

        private string CommandName
        {
            get
            {
                if (!_isUsable)
                {
                    throw new InvalidOperationException($"Container engine is not usable: {_unusableReason}");
                }
                return _isPodman ? "podman" : "docker";
            }
        }

        public bool IsUsable(out string? unusableReason)
        {
            unusableReason = _unusableReason;
            return _isUsable;
        }

        public ContainerEngine(ContainerEngineType? containerEngine)
        {
            _isUsable = true;
            _aspnetUrlsHost = "localhost";
            if ((!containerEngine.HasValue || containerEngine == ContainerEngineType.Podman) &&
                TryUsePodman(ref _unusableReason, ref _containerHost))
            {
                _isPodman = true;
                return;
            }
            if ((!containerEngine.HasValue || containerEngine == ContainerEngineType.Docker) &&
                TryUseDocker(ref _unusableReason, ref _containerHost, ref _aspnetUrlsHost))
            {
                return;
            }
            _isUsable = false;
            _unusableReason = "container engine is not installed.";
        }

        private static bool TryUsePodman(ref string? unusableReason, ref string? containerHost)
        {
            ProcessResult result;
            try
            {
                result = ProcessUtil.RunAsync("podman", "version -f \"{{ .Client.Version }}\"", throwOnError: false, cancellationToken: new CancellationTokenSource(Timeout).Token).Result;
            }
            catch
            {
                return false;
            }

            if (result.ExitCode != 0)
            {
                unusableReason = $"podman version exited with {result.ExitCode}. Standard error: \"{result.StandardError}\".";
                return true;
            }

            Version minVersion = new Version(3, 1);
            if (Version.TryParse(result.StandardOutput, out Version? version) &&
                version < minVersion)
            {
                unusableReason = $"podman version '{result.StandardOutput}' is less than the required '{minVersion}'.";
                return true;
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
                containerHost = "10.0.2.2";
            }
            return true;
        }

        private static bool TryUseDocker(ref string? unusableReason, ref string? containerHost, ref string aspnetUrlsHost)
        {
            ProcessResult result;
            try
            {
                result = ProcessUtil.RunAsync("docker", "version", throwOnError: false, cancellationToken: new CancellationTokenSource(Timeout).Token).Result;
            }
            catch
            {
                return false;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // See: https://github.com/docker/for-linux/issues/264
                //
                // host.docker.internal is making it's way into linux docker but doesn't work yet
                // instead we use the machine IP
                var addresses = Dns.GetHostAddresses(Dns.GetHostName());
                containerHost = addresses[0].ToString();

                // We need to bind to all interfaces on linux since the container -> host communication won't work
                // if we use the IP address to reach out of the host. This works fine on osx and windows
                // but doesn't work on linux.
                aspnetUrlsHost = "*";
            }
            else
            {
                containerHost = "host.docker.internal";
            }

            if (result.ExitCode != 0)
            {
                unusableReason = "docker is not connected.";
            }

            return true;
        }
    }
}
