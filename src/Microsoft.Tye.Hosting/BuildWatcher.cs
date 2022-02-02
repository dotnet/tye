// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Extensions.Logging;

namespace Microsoft.Tye.Hosting
{
    internal sealed class BuildWatcher : IAsyncDisposable
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ILogger _logger;
        private Task? _processor;
        private Channel<BuildRequest>? _queue;

        public BuildWatcher(ILogger logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(string? solutionPath, string workingDirectory)
        {
            await StopAsync();

            _queue = Channel.CreateUnbounded<BuildRequest>();
            _cancellationTokenSource = new CancellationTokenSource();
            _processor = Task.Run(() => ProcessTaskQueueAsync(_logger, _queue.Reader, solutionPath, workingDirectory, _cancellationTokenSource.Token));
        }

        public async Task StopAsync()
        {
            _queue?.Writer.TryComplete();
            _queue = null;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            if (_processor != null)
            {
                await _processor;

                _processor = null;
            }
        }

        public async Task<int> BuildProjectFileAsync(string projectFilePath)
        {
            if (_queue == null)
            {
                throw new InvalidOperationException("The worker is not running.");
            }

            var buildRequest = new BuildRequest(projectFilePath);

            await _queue.Writer.WriteAsync(buildRequest);

            return await buildRequest.Task;
        }

        #region IAsyncDisposable Members

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        #endregion

        private static string GetProjectName(SolutionFile solution, string projectFile)
        {
            foreach (var project in solution.ProjectsInOrder)
            {
                if (project.AbsolutePath == projectFile)
                {
                    return project.ProjectName;
                }
            }

            throw new InvalidOperationException($"Could not find project in solution: {projectFile}");
        }

        private static async Task ProcessTaskQueueAsync(
            ILogger logger,
            ChannelReader<BuildRequest> requestReader,
            string? solutionPath,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Build Watcher: Watching for builds...");

            try
            {
                while (await requestReader.WaitToReadAsync(cancellationToken))
                {
                    var delay = TimeSpan.FromMilliseconds(250);

                    logger.LogInformation("Build Watcher: Builds requested; waiting {DelayInMs}ms for more...", delay.TotalMilliseconds);

                    await Task.Delay(delay);

                    logger.LogInformation("Build Watcher: Getting requests...");

                    var requests = new List<BuildRequest>();

                    while (requestReader.TryRead(out var request))
                    {
                        requests.Add(request);
                    }

                    logger.LogInformation("Build Watcher: Processing {Count} requests...", requests.Count);

                    var solution = (solutionPath != null) ? SolutionFile.Parse(solutionPath) : null;

                    var solutionBatch = new Dictionary<string, List<BuildRequest>>(); //  store the list of promises
                    var projectBatch = new Dictionary<string, List<BuildRequest>>();

                    foreach (var request in requests)
                    {
                        if (solution?.ProjectShouldBuild(request.ProjectFilePath) == true)
                        {
                            if (!solutionBatch.ContainsKey(request.ProjectFilePath))
                            {
                                solutionBatch.Add(request.ProjectFilePath, new List<BuildRequest>());
                            }

                            solutionBatch[request.ProjectFilePath].Add(request);
                        }
                        else
                        {
                            // this will also prevent us building multiple times if a project is used by multiple services
                            if (!projectBatch.ContainsKey(request.ProjectFilePath))
                            {
                                projectBatch.Add(request.ProjectFilePath, new List<BuildRequest>());
                            }

                            projectBatch[request.ProjectFilePath].Add(request);
                        }
                    }

                    async Task WithRequestCompletion(IEnumerable<BuildRequest> requests, Func<Task<int>> buildFunc)
                    {
                        try
                        {
                            int exitCode = await buildFunc();

                            foreach (var request in requests)
                            {
                                request.Complete(exitCode);
                            }
                        }
                        catch (Exception ex)
                        {
                            foreach (var request in requests)
                            {
                                request.Complete(ex);
                            }
                        }
                    }

                    var tasks = new List<Task>();

                    if (solutionBatch.Any())
                    {
                        var targets = String.Join(",", solutionBatch.Keys.Select(key => GetProjectName(solution!, key)));

                        tasks.Add(
                            WithRequestCompletion(
                                solutionBatch.Values.SelectMany(x => x),
                                async () =>
                                {
                                    logger.LogInformation("Build Watcher: Building {Targets} of solution {SolutionPath}...", targets, solutionPath);

                                    var buildResult = await ProcessUtil.RunAsync("dotnet", $"msbuild {solutionPath} -target:{targets}", throwOnError: false, workingDirectory: workingDirectory, cancellationToken: cancellationToken);

                                    if (buildResult.ExitCode != 0)
                                    {
                                        logger.LogInformation("Build Watcher: Solution build failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, buildResult.ExitCode);
                                    }

                                    return buildResult.ExitCode;
                                }));
                    }

                    foreach (var project in projectBatch)
                    {
                        tasks.Add(
                            WithRequestCompletion(
                                project.Value,
                                async () =>
                                {
                                    logger.LogInformation("Build Watcher: Building project {ProjectPath}...", project.Key);

                                    var buildResult = await ProcessUtil.RunAsync("dotnet", $"build \"{project.Key}\" /nologo", throwOnError: false, workingDirectory: workingDirectory, cancellationToken: cancellationToken);

                                    if (buildResult.ExitCode != 0)
                                    {
                                        logger.LogInformation("Build Watcher: Project build failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, buildResult.ExitCode);
                                    }

                                    return buildResult.ExitCode;
                                }));
                    }

                    logger.LogInformation("Build Watcher: Waiting for builds to complete...");

                    // NOTE: WithRequestCompletion() will trap exceptions so build errors should not bubble up from WhenAll().

                    await Task.WhenAll(tasks);

                    logger.LogInformation("Build Watcher: Done with requests; waiting for more...");
                }
            }
            catch (OperationCanceledException)
            {
                // NO-OP: Trap exception due to cancellation.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Build Watcher: Error while processing builds.");
            }

            logger.LogInformation("Build Watcher: Done watching.");
        }

        private class BuildRequest
        {
            private readonly TaskCompletionSource<int> _result = new TaskCompletionSource<int>();

            public BuildRequest(string projectFilePath)
            {
                ProjectFilePath = projectFilePath;
            }

            public string ProjectFilePath { get; }

            public Task<int> Task => _result.Task;

            public void Complete(int exitCode)
            {
                _result.TrySetResult(exitCode);
            }

            public void Complete(Exception ex)
            {
                _result.TrySetException(ex);
            }
        }
    }
}
