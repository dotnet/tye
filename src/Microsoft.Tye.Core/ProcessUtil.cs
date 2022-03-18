// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public static class ProcessUtil
    {
        #region Native Methods

        [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
        private static extern int sys_kill(int pid, int sig);

        #endregion

        private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private const int ProcessExitTimeoutMs = 30 * 1000; // 30 seconds timeout for the process to exit.

        public static Task<int> ExecuteAsync(
            string command,
            string args,
            string? workingDir = null,
            Action<string>? stdOut = null,
            Action<string>? stdErr = null,
            params (string key, string value)[] environmentVariables)
        {
            return System.CommandLine.Invocation.Process.ExecuteAsync(command, args, workingDir, stdOut, stdErr, environmentVariables);
        }

        public static async Task<ProcessResult> RunAsync(
            string filename,
            string arguments,
            string? workingDirectory = null,
            bool throwOnError = true,
            IDictionary<string, string>? environmentVariables = null,
            Action<string>? outputDataReceived = null,
            Action<string>? errorDataReceived = null,
            Action<int>? onStart = null,
            Action<int>? onStop = null,
            CancellationToken cancellationToken = default)
        {
            using var process = new Process()
            {
                StartInfo =
                {
                    FileName = filename,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = !IsWindows,
                    WindowStyle = ProcessWindowStyle.Hidden
                },
                EnableRaisingEvents = true
            };

            if (workingDirectory != null)
            {
                process.StartInfo.WorkingDirectory = workingDirectory;
            }

            if (environmentVariables != null)
            {
                foreach (var kvp in environmentVariables)
                {
                    process.StartInfo.Environment.Add(kvp!);
                }
            }

            var outputLock = new SpinLock();

            void WithOutputLock(Action action)
            {
                bool gotLock = false;

                try
                {
                    outputLock.Enter(ref gotLock);

                    action();
                }
                finally
                {
                    if (gotLock)
                    {
                        outputLock.Exit();
                    }
                }
            }

            var outputBuilder = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                if (outputDataReceived != null)
                {
                    outputDataReceived.Invoke(e.Data);
                }
                else
                {
                    WithOutputLock(() => outputBuilder.AppendLine(e.Data));
                }
            };

            var errorBuilder = new StringBuilder();
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                if (errorDataReceived != null)
                {
                    errorDataReceived.Invoke(e.Data);
                }
                else
                {
                    WithOutputLock(() => errorBuilder.AppendLine(e.Data));
                }
            };

            var processLifetimeTask = new TaskCompletionSource<ProcessResult>();

            process.Exited += (_, e) =>
            {
                lock (process)
                {
                    // Even though the Exited event has been raised, WaitForExit() must still be called to ensure the output buffers
                    // have been flushed before the process is considered completely done.
                    // Because of the bug in the dotnet runtime https://github.com/dotnet/runtime/issues/29232, Process.WaitForExit()
                    // hangs for processes that spawn another long-running processes.
                    // Since these are expected to be long running processes and we're typically not concerned with capturing all of its output
                    // i.e. it's probably ok for some output to be lost on shutdown, since Tye is shutting down anyway,
                    // we call Process.WaitForProcessExit(ProcessExitTimeoutMs).
                    // Also, since this is a process.Exited event, process.ExitCode is valid even if WaitForExit() times out.
                    process.WaitForExit(ProcessExitTimeoutMs);
                }

                // NOTE: If WaitForExit() returns false, more output may be written,
                //       so we must synchronize access to the output StringBuilders.

                WithOutputLock(
                    () =>
                    {
                        if (throwOnError && process.ExitCode != 0)
                        {
                            processLifetimeTask.TrySetException(new InvalidOperationException($"Command {filename} {arguments} returned exit code {process.ExitCode}. Standard error: \"{errorBuilder.ToString()}\""));
                        }
                        else
                        {
                            processLifetimeTask.TrySetResult(new ProcessResult(outputBuilder.ToString(), errorBuilder.ToString(), process.ExitCode));
                        }
                    });
            };

            // lock ensures we're reading output when WaitForExit is called in process.Exited event.
            lock (process)
            {
                process.Start();
                onStart?.Invoke(process.Id);

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            var cancelledTcs = new TaskCompletionSource<object?>();
            await using var _ = cancellationToken.Register(() => cancelledTcs.TrySetResult(null));

            var result = await Task.WhenAny(processLifetimeTask.Task, cancelledTcs.Task);

            if (result == cancelledTcs.Task)
            {
                if (!IsWindows)
                {
                    sys_kill(process.Id, sig: 2); // SIGINT
                }
                else
                {
                    if (!process.CloseMainWindow())
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }

                if (!process.HasExited)
                {
                    var cancel = new CancellationTokenSource();
                    await Task.WhenAny(processLifetimeTask.Task, Task.Delay(TimeSpan.FromSeconds(5), cancel.Token));
                    cancel.Cancel();

                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
            }

            var processResult = await processLifetimeTask.Task;
            onStop?.Invoke(processResult.ExitCode);
            return processResult;
        }

        public static Task<ProcessResult> RunAsync(ProcessSpec processSpec, CancellationToken cancellationToken = default, bool throwOnError = true)
        {
            return RunAsync(processSpec.Executable!, processSpec.Arguments!, processSpec.WorkingDirectory, throwOnError: throwOnError, processSpec.EnvironmentVariables, processSpec.OutputData, processSpec.ErrorData, processSpec.OnStart, processSpec.OnStop, cancellationToken);
        }

        public static void KillProcess(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                process?.Kill(entireProcessTree: true);
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }
    }
}
