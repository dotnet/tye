// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Construction;
using System.Threading;

namespace Microsoft.Tye.Hosting
{
    internal sealed class WatchBuilderWorker : IAsyncDisposable
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ILogger _logger;
        private Task? _processor;
        private Channel<BuildRequest>? _queue;

        public WatchBuilderWorker(ILogger logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(string? solutionPath)
        {
            await StopAsync();

            _queue = Channel.CreateUnbounded<BuildRequest>();
            _cancellationTokenSource = new CancellationTokenSource();
            _processor = Task.Run(() => ProcessTaskQueueAsync(_logger, _queue.Reader, solutionPath, _cancellationTokenSource.Token));
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

        public async Task<int> BuildProjectFileAsync(string projectFilePath, string workingDirectory) {
            if (_queue == null)
            {
                throw new InvalidOperationException("The worker is not running.");
            }

            var buildRequest = new BuildRequest(projectFilePath, workingDirectory);

            await _queue.Writer.WriteAsync(buildRequest);

            return await buildRequest.Task;
        }

        #region IAsyncDisposable Members

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        #endregion

        private static Task<int> BuildProjectFileAsyncImpl(ILogger logger, string projectFilePath, string workingDirectory) {
            logger.LogInformation($"Building project ${projectFilePath}...");
            return ProcessUtil.RunAsync("dotnet", $"build \"{projectFilePath}\" /nologo", throwOnError: false, workingDirectory: workingDirectory)
                .ContinueWith((processTask) => {
                    var buildResult = processTask.Result;
                    if (buildResult.ExitCode != 0)
                    {
                        logger.LogInformation("Building projects failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, buildResult.ExitCode);
                    }
                    return buildResult.ExitCode;
                });
        }

        private static string GetProjectName(SolutionFile solution, string projectFile)
        {
            foreach(var project in solution.ProjectsInOrder) {
                if(project.AbsolutePath == projectFile)
                {
                    return project.ProjectName;
                }
            }
            // TODO: error
            return "";
        }

        private static async Task ProcessTaskQueueAsync(
            ILogger logger,
            ChannelReader<BuildRequest> requestReader,
            string? solutionPath,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Build Watcher: Watching for builds...");

            try
            {
                while (await requestReader.WaitToReadAsync(cancellationToken))
                {
                    var solutionBatch = new Dictionary<string, List<BuildRequest>>(); //  store the list of promises
                    var projectBatch = new Dictionary<string, List<BuildRequest>>();
                    // TODO: quiet time... maybe wait both...?
                    await Task.Delay(100);

                    var solution = (solutionPath != null) ? SolutionFile.Parse(solutionPath) : null;
                    string targets = "";
                    string workingDirectory = ""; // FIXME: should be set in the worker constructor
                    while (requestReader.TryRead(out BuildRequest? item))
                    {
                        try {
                            if(workingDirectory.Length == 0)
                            {
                                workingDirectory = item.WorkingDirectory;
                            }
                            if(solution != null && solution.ProjectShouldBuild(item.ProjectFilePath))
                            {
                                if(!solutionBatch.ContainsKey(item.ProjectFilePath))
                                {
                                    if(targets.Length > 0)
                                    {
                                        targets += ",";
                                    }
                                    targets += GetProjectName(solution, item.ProjectFilePath); // note, assuming the default target is Build
                                    solutionBatch.Add(item.ProjectFilePath, new List<BuildRequest>());
                                }
                                solutionBatch[item.ProjectFilePath].Add(item);
                            }
                            else
                            {
                                // this will also prevent us building multiple times if a project is used by multiple services
                                if(!projectBatch.ContainsKey(item.ProjectFilePath))
                                {
                                    projectBatch.Add(item.ProjectFilePath, new List<BuildRequest>());
                                }
                                projectBatch[item.ProjectFilePath].Add(item);
                            }
                        }
                        catch (Exception)
                        {
                            item.Complete(-1);
                        }
                    }

                    var tasks = new List<Task>();
                    if(solutionBatch.Count > 0)
                    {
                        tasks.Add(Task.Run(async () => {
                            logger.LogInformation("Building projects from solution: " + targets);
                            int exitCode = -1;
                            try
                            {
                                var buildResult = await ProcessUtil.RunAsync("dotnet", $"msbuild {solutionPath} -target:{targets}", throwOnError: false, workingDirectory: workingDirectory);
                                if (buildResult.ExitCode != 0)
                                {
                                    logger.LogInformation("Building solution failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, buildResult.ExitCode);
                                }
                                exitCode = buildResult.ExitCode;
                            }
                            finally {
                                foreach(var project in solutionBatch)
                                {
                                    foreach(var buildRequest in project.Value)
                                    {
                                        buildRequest.Complete(exitCode);
                                    }
                                }
                            }
                        }));
                    }
                    else
                    {
                        foreach(var project in projectBatch)
                        {
                            // FIXME: this is serial
                            tasks.Add(Task.Run(async () => {
                                var exitCode = await BuildProjectFileAsyncImpl(logger, project.Key, workingDirectory);
                                foreach(var buildRequest in project.Value)
                                {
                                    buildRequest.Complete(exitCode);
                                }
                            }));
                        }
                    }

                    Task.WaitAll(tasks.ToArray());
                }
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred executing task work item.");
            }

            logger.LogInformation("Build Watcher: Done watching.");
        }

        private class BuildRequest
        {
            private readonly TaskCompletionSource<int> _result = new TaskCompletionSource<int>();

            public BuildRequest(string projectFilePath, string workingDirectory)
            {
                ProjectFilePath = projectFilePath;
                WorkingDirectory = workingDirectory;
            }

            public string ProjectFilePath { get; }

            public string WorkingDirectory { get; }

            public Task<int> Task => _result.Task;

            public void Complete(int exitCode)
            {
                if(!_result.TrySetResult(exitCode))
                {
                    throw new Exception("failed to set result");
                }
            }
        }
    }
}