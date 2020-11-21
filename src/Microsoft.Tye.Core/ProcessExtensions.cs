// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Tye
{
    internal static class ProcessExtensions
    {
        private static readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);

        public static void KillTree(this System.Diagnostics.Process process) => process.KillTree(_defaultTimeout);

        public static void KillTree(this System.Diagnostics.Process process, TimeSpan timeout)
        {
            var pid = process.Id;
            if (_isWindows)
            {
                RunProcessAndWaitForExit(
                    "taskkill",
                    $"/T /F /PID {pid}",
                    timeout,
                    out _);
            }
            else
            {
                var children = new HashSet<int>();
                GetAllChildIdsUnix(pid, children, timeout);
                foreach (var childId in children)
                {
                    KillProcessUnix(childId, timeout);
                }
                KillProcessUnix(pid, timeout);
            }
        }

        private static void GetAllChildIdsUnix(int parentId, ISet<int> children, TimeSpan timeout)
        {
            try
            {
                RunProcessAndWaitForExit(
                    "pgrep",
                    $"-P {parentId}",
                    timeout,
                    out var stdout);

                if (string.IsNullOrEmpty(stdout))
                {
                    return;
                }

                using var reader = new StringReader(stdout);
                while (true)
                {
                    var text = reader.ReadLine();
                    if (text == null)
                    {
                        return;
                    }

                    if (!int.TryParse(text, out var id))
                    {
                        continue;
                    }

                    children.Add(id);
                    // Recursively get the children
                    GetAllChildIdsUnix(id, children, timeout);
                }
            }
            catch (Win32Exception ex) when (ex.Message.Contains("No such file or directory"))
            {
                // This probably means that pgrep isn't installed. Nothing to be done?
            }
        }

        private static void KillProcessUnix(int processId, TimeSpan timeout)
        {
            try
            {
                RunProcessAndWaitForExit(
                    "kill",
                    $"-TERM {processId}",
                    timeout,
                    out var stdout);
            }
            catch (Win32Exception ex) when (ex.Message.Contains("No such file or directory"))
            {
                // This probably means that the process is already dead
            }
        }

        private static void RunProcessAndWaitForExit(string fileName, string arguments, TimeSpan timeout, out string? stdout)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            var process = Process.Start(startInfo);

            stdout = null;
            if (process?.WaitForExit((int)timeout.TotalMilliseconds) == true)
            {
                stdout = process.StandardOutput.ReadToEnd();
            }
            else
            {
                process?.Kill();
            }
        }
    }
}
