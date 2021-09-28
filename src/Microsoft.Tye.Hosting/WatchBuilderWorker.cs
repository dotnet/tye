using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Build.Construction;

namespace Microsoft.Tye.Hosting
{
    class WatchBuilderWorker
    {
        private readonly ILogger _logger;
        private readonly Channel<BuildRequest> _queue;
        private Task _processor;
        private string? _solutionPath;

        public string? SolutionPath { get => _solutionPath; set => _solutionPath = value; }

        public WatchBuilderWorker(ILogger logger) 
        {
            _logger = logger; 
            _queue = Channel.CreateUnbounded<BuildRequest>(); 
            _processor = Task.Run(ProcessTaskQueueAsync);
        }

        class BuildRequest
        {
            public BuildRequest(string projectFilePath, string workingDirectory)
            {
                this.projectFilePath = projectFilePath;
                this.workingDirectory = workingDirectory;
            }

            public string projectFilePath { get; set; }

            public string workingDirectory { get; set; }

            private TaskCompletionSource<int> _result = new TaskCompletionSource<int>();

            public Task<int> task()
            {
                return _result.Task;
            }

            public void complete(int exitCode)
            {
                if(!_result.TrySetResult(exitCode))
                    throw new Exception("failed to set result");
            }
        }

        public Task<int> buildProjectFileAsync(string projectFilePath, string workingDirectory) {
            var buildRequest = new BuildRequest(projectFilePath, workingDirectory);
            _queue.Writer.WriteAsync(buildRequest);
            return buildRequest.task();
        }

        public Task<int> buildProjectFileAsyncImpl(string projectFilePath, string workingDirectory) {
            _logger.LogInformation($"Building project ${projectFilePath}...");
            return ProcessUtil.RunAsync("dotnet", $"build \"{projectFilePath}\" /nologo", throwOnError: false, workingDirectory: workingDirectory)
                .ContinueWith((processTask) => {
                    var buildResult = processTask.Result;
                    if (buildResult.ExitCode != 0)
                    {
                        _logger.LogInformation("Building projects failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, buildResult.ExitCode);
                    }
                    return buildResult.ExitCode;
                });
        }

        private string GetProjectName(SolutionFile solution, string projectFile)
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

        private async Task ProcessTaskQueueAsync()
        {
            try
            {
                while (await _queue.Reader.WaitToReadAsync())
                {
                    var solutionBatch = new Dictionary<string, List<BuildRequest>>(); //  store the list of promises
                    var projectBatch = new Dictionary<string, List<BuildRequest>>();
                    // TODO: quiet time... maybe wait both...?
                    await Task.Delay(100);

                    var solution = (SolutionPath != null) ? SolutionFile.Parse(SolutionPath) : null;
                    string targets = ""; 
                    string workingDirectory = ""; // FIXME: should be set in the worker constructor
                    while (_queue.Reader.TryRead(out BuildRequest item))
                    {
                        try {
                            if(workingDirectory.Length == 0)
                            {
                                workingDirectory = item.workingDirectory;
                            }
                            if(solution != null && solution.ProjectShouldBuild(item.projectFilePath))
                            {
                                if(!solutionBatch.ContainsKey(item.projectFilePath))
                                {
                                    if(targets.Length > 0)
                                    {
                                        targets += ",";
                                    }
                                    targets += GetProjectName(solution, item.projectFilePath);
                                    solutionBatch.Add(item.projectFilePath, new List<BuildRequest>());
                                }
                                solutionBatch[item.projectFilePath].Add(item);
                            }
                            else
                            {
                                // this will also prevent us building multiple times if a project is used by multiple services
                                if(!projectBatch.ContainsKey(item.projectFilePath))
                                {
                                    projectBatch.Add(item.projectFilePath, new List<BuildRequest>());
                                }
                                projectBatch[item.projectFilePath].Add(item);
                            }
                        }
                        catch (Exception e)
                        {
                            item.complete(-1);
                        }
                    }

                    var tasks = new List<Task>();
                    if(solutionBatch.Count > 0)
                    {
                        tasks.Add(Task.Run(async () => {
                            _logger.LogInformation("Building projects from solution: " + targets);
                            var buildResult = await ProcessUtil.RunAsync("dotnet", "msbuild -targets:" + targets, throwOnError: false, workingDirectory: workingDirectory);
                            if (buildResult.ExitCode != 0)
                            {
                                _logger.LogInformation("Building solution failed with exit code {ExitCode}: \r\n" + buildResult.StandardOutput, buildResult.ExitCode);
                            }
                            foreach(var project in solutionBatch)
                            {
                                foreach(var buildRequest in project.Value)
                                {
                                    buildRequest.complete(buildResult.ExitCode);
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
                                var exitCode = await buildProjectFileAsyncImpl(project.Key, workingDirectory);
                                foreach(var buildRequest in project.Value)
                                {
                                    buildRequest.complete(exitCode);
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
                _logger.LogError(ex, "Error occurred executing task work item.");
            }
        }
    }
}