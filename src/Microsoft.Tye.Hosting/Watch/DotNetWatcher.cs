// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watcher
{
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
            cancellationToken.Register(state => ((TaskCompletionSource<object>)state!).TrySetResult(null!),
                cancelledTaskSource);

            var iteration = 1;

            while (true)
            {
                processSpec.EnvironmentVariables["DOTNET_WATCH_ITERATION"] = iteration.ToString(CultureInfo.InvariantCulture);
                iteration++;

                var fileSet = await fileSetFactory.CreateAsync(cancellationToken);

                if (fileSet == null)
                {
                    _logger.LogError("watch: Failed to find a list of files to watch");
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

                    var args = ArgumentEscaper.EscapeAndConcatenate(processSpec.Arguments!);
                    _logger.LogDebug($"Running {processSpec.ShortDisplayName()} with the following arguments: {args}");

                    _logger.LogInformation("watch: {Replica} Started", processSpec.Replica);

                    var finishedTask = await Task.WhenAny(processTask, fileSetTask, cancelledTaskSource.Task);

                    // Regardless of the which task finished first, make sure everything is cancelled
                    // and wait for dotnet to exit. We don't want orphan processes
                    currentRunCancellationSource.Cancel();

                    await Task.WhenAll(processTask, fileSetTask);

                    if (processTask.Result != 0 && finishedTask == processTask && !cancellationToken.IsCancellationRequested)
                    {
                        // Only show this error message if the process exited non-zero due to a normal process exit.
                        // Don't show this if dotnet-watch killed the inner process due to file change or CTRL+C by the user
                        _logger.LogError($"watch: Exited with error code {processTask.Result}");
                    }
                    else
                    {
                        _logger.LogInformation("watch: {Replica} Exited", processSpec.Replica);
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
                        _logger.LogInformation($"watch: File changed: {fileSetTask.Result}");
                    }
                }
            }
        }
    }
}
