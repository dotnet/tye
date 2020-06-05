// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watcher
{
    public interface IConsole
    {
        event ConsoleCancelEventHandler CancelKeyPress;
        TextWriter Out { get; }
        TextWriter Error { get; }
        TextReader In { get; }
        bool IsInputRedirected { get; }
        bool IsOutputRedirected { get; }
        bool IsErrorRedirected { get; }
        ConsoleColor ForegroundColor { get; set; }
        void ResetColor();
    }

    internal static class Ensure
    {
        public static T NotNull<T>(T obj, string paramName)
            where T : class
        {
            if (obj == null)
            {
                throw new ArgumentNullException(paramName);
            }
            return obj;
        }

        public static string NotNullOrEmpty(string obj, string paramName)
        {
            if (string.IsNullOrEmpty(obj))
            {
                throw new ArgumentException("Value cannot be null or an empty string.", paramName);
            }
            return obj;
        }
    }
    internal static class ArgumentEscaper
    {
        /// <summary>
        /// Undo the processing which took place to create string[] args in Main, so that the next process will
        /// receive the same string[] args.
        /// </summary>
        /// <remarks>
        /// See https://blogs.msdn.microsoft.com/twistylittlepassagesallalike/2011/04/23/everyone-quotes-command-line-arguments-the-wrong-way/
        /// </remarks>
        /// <param name="args">The arguments to concatenate.</param>
        /// <returns>The escaped arguments, concatenated.</returns>
        public static string EscapeAndConcatenate(IEnumerable<string> args)
            => string.Join(" ", args.Select(EscapeSingleArg));

        private static string EscapeSingleArg(string arg)
        {
            var sb = new StringBuilder();

            var needsQuotes = ShouldSurroundWithQuotes(arg);
            var isQuoted = needsQuotes || IsSurroundedWithQuotes(arg);

            if (needsQuotes)
            {
                sb.Append('"');
            }

            for (int i = 0; i < arg.Length; ++i)
            {
                var backslashes = 0;

                // Consume all backslashes
                while (i < arg.Length && arg[i] == '\\')
                {
                    backslashes++;
                    i++;
                }

                if (i == arg.Length && isQuoted)
                {
                    // Escape any backslashes at the end of the arg when the argument is also quoted.
                    // This ensures the outside quote is interpreted as an argument delimiter
                    sb.Append('\\', 2 * backslashes);
                }
                else if (i == arg.Length)
                {
                    // At then end of the arg, which isn't quoted,
                    // just add the backslashes, no need to escape
                    sb.Append('\\', backslashes);
                }
                else if (arg[i] == '"')
                {
                    // Escape any preceding backslashes and the quote
                    sb.Append('\\', (2 * backslashes) + 1);
                    sb.Append('"');
                }
                else
                {
                    // Output any consumed backslashes and the character
                    sb.Append('\\', backslashes);
                    sb.Append(arg[i]);
                }
            }

            if (needsQuotes)
            {
                sb.Append('"');
            }

            return sb.ToString();
        }

        private static bool ShouldSurroundWithQuotes(string argument)
        {
            // Don't quote already quoted strings
            if (IsSurroundedWithQuotes(argument))
            {
                return false;
            }

            // Only quote if whitespace exists in the string
            return ContainsWhitespace(argument);
        }

        private static bool IsSurroundedWithQuotes(string argument)
        {
            if (argument.Length <= 1)
            {
                return false;
            }

            return argument[0] == '"' && argument[argument.Length - 1] == '"';
        }

        private static bool ContainsWhitespace(string argument)
            => argument.IndexOfAny(new[] { ' ', '\t', '\n' }) >= 0;
    }

    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class PhysicalConsole : IConsole
    {
        private PhysicalConsole()
        {
            Console.CancelKeyPress += (o, e) =>
            {
                CancelKeyPress?.Invoke(o, e);
            };
        }

        public static IConsole Singleton { get; } = new PhysicalConsole();

        public event ConsoleCancelEventHandler CancelKeyPress;
        public TextWriter Error => Console.Error;
        public TextReader In => Console.In;
        public TextWriter Out => Console.Out;
        public bool IsInputRedirected => Console.IsInputRedirected;
        public bool IsOutputRedirected => Console.IsOutputRedirected;
        public bool IsErrorRedirected => Console.IsErrorRedirected;
        public ConsoleColor ForegroundColor
        {
            get => Console.ForegroundColor;
            set => Console.ForegroundColor = value;
        }

        public void ResetColor() => Console.ResetColor();
    }

    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public static class CliContext
    {
        /// <summary>
        /// dotnet -d|--diagnostics subcommand
        /// </summary>
        /// <returns></returns>
        public static bool IsGlobalVerbose()
        {
            bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE"), out bool globalVerbose);
            return globalVerbose;
        }
    }

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
                    out var _);
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

                if (!string.IsNullOrEmpty(stdout))
                {
                    using (var reader = new StringReader(stdout))
                    {
                        while (true)
                        {
                            var text = reader.ReadLine();
                            if (text == null)
                            {
                                return;
                            }

                            if (int.TryParse(text, out var id))
                            {
                                children.Add(id);
                                // Recursively get the children
                                GetAllChildIdsUnix(id, children, timeout);
                            }
                        }
                    }
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

        private static void RunProcessAndWaitForExit(string fileName, string arguments, TimeSpan timeout, out string stdout)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            var process = System.Diagnostics.Process.Start(startInfo);

            stdout = null;
            if (process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                stdout = process.StandardOutput.ReadToEnd();
            }
            else
            {
                process.Kill();
            }
        }
    }

    internal static class DotNetMuxer
    {
        private const string MuxerName = "dotnet";

        static DotNetMuxer()
        {
            MuxerPath = TryFindMuxerPath();
        }

        /// <summary>
        /// The full filepath to the .NET Core muxer.
        /// </summary>
        public static string MuxerPath { get; }

        /// <summary>
        /// Finds the full filepath to the .NET Core muxer,
        /// or returns a string containing the default name of the .NET Core muxer ('dotnet').
        /// </summary>
        /// <returns>The path or a string named 'dotnet'.</returns>
        public static string MuxerPathOrDefault()
            => MuxerPath ?? MuxerName;

        private static string TryFindMuxerPath()
        {
            var fileName = MuxerName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fileName += ".exe";
            }

            var mainModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
            if (!string.IsNullOrEmpty(mainModule?.FileName)
                && Path.GetFileName(mainModule.FileName).Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                return mainModule.FileName;
            }

            return null;
        }
    }

    public class DotNetWatcher
    {
        private readonly ProcessRunner _processRunner;
        private readonly ILogger _logger;

        public DotNetWatcher(ILogger logger)
        {
            _processRunner = new ProcessRunner(logger);
            _logger = logger;
        }

        public async Task WatchAsync(ProcessSpec processSpec, IFileSetFactory fileSetFactory,
            CancellationToken cancellationToken)
        {
            Ensure.NotNull(processSpec, nameof(processSpec));

            var cancelledTaskSource = new TaskCompletionSource<object>();
            cancellationToken.Register(state => ((TaskCompletionSource<object>) state).TrySetResult(null),
                cancelledTaskSource);

            var iteration = 1;

            while (true)
            {
                processSpec.EnvironmentVariables["DOTNET_WATCH_ITERATION"] = iteration.ToString(CultureInfo.InvariantCulture);
                iteration++;

                var fileSet = await fileSetFactory.CreateAsync(cancellationToken);

                if (fileSet == null)
                {
                    _logger.LogError("Failed to find a list of files to watch");
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                using (var currentRunCancellationSource = new CancellationTokenSource())
                using (var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    currentRunCancellationSource.Token))
                using (var fileSetWatcher = new FileSetWatcher(fileSet, _logger))
                {
                    var fileSetTask = fileSetWatcher.GetChangedFileAsync(combinedCancellationSource.Token);
                    var processTask = _processRunner.RunAsync(processSpec, combinedCancellationSource.Token);

                    var args = ArgumentEscaper.EscapeAndConcatenate(processSpec.Arguments);
                    _logger.LogDebug($"Running {processSpec.ShortDisplayName()} with the following arguments: {args}");

                    _logger.LogInformation("Started");

                    var finishedTask = await Task.WhenAny(processTask, fileSetTask, cancelledTaskSource.Task);

                    // Regardless of the which task finished first, make sure everything is cancelled
                    // and wait for dotnet to exit. We don't want orphan processes
                    currentRunCancellationSource.Cancel();

                    await Task.WhenAll(processTask, fileSetTask);

                    if (processTask.Result != 0 && finishedTask == processTask && !cancellationToken.IsCancellationRequested)
                    {
                        // Only show this error message if the process exited non-zero due to a normal process exit.
                        // Don't show this if dotnet-watch killed the inner process due to file change or CTRL+C by the user
                        _logger.LogError($"Exited with error code {processTask.Result}");
                    }
                    else
                    {
                        _logger.LogInformation("Exited");
                    }

                    if (finishedTask == cancelledTaskSource.Task || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (finishedTask == processTask)
                    {
                        // Now wait for a file to change before restarting process
                        await fileSetWatcher.GetChangedFileAsync(cancellationToken, () => _logger.LogWarning("Waiting for a file to change before restarting dotnet..."));
                    }

                    if (!string.IsNullOrEmpty(fileSetTask.Result))
                    {
                        _logger.LogInformation($"File changed: {fileSetTask.Result}");
                    }
                }
            }
        }
    }
}
