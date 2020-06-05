// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watcher.Internal
{
    public class MsBuildFileSetFactory : IFileSetFactory
    {
        private const string TargetName = "GenerateWatchList";
        private const string WatchTargetsFileName = "DotNetWatch.targets";
        private readonly ILogger _logger;
        private readonly string _projectFile;
        private readonly ProcessRunner _processRunner;
        private readonly bool _waitOnError;
        private readonly IReadOnlyList<string> _buildFlags;

        public MsBuildFileSetFactory(ILogger reporter,
            string projectFile,
            bool waitOnError,
            bool trace)
            : this(reporter, projectFile, trace)
        {
            _waitOnError = waitOnError;
        }

        // output sink is for testing
        internal MsBuildFileSetFactory(ILogger logger,
            string projectFile,
            bool trace)
        {
            Ensure.NotNull(logger, nameof(logger));
            Ensure.NotNullOrEmpty(projectFile, nameof(projectFile));

            _logger = logger;
            _projectFile = projectFile;
            _processRunner = new ProcessRunner(logger);
            _buildFlags = InitializeArgs(FindTargetsFile(), trace);
        }

        public async Task<IFileSet> CreateAsync(CancellationToken cancellationToken)
        {
            var watchList = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                var projectDir = Path.GetDirectoryName(_projectFile);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // TODO adding files doesn't currently work. Need to provide a way to detect new files
                    // find files
                    var processSpec = new ProcessSpec
                    {
                        Executable = DotNetMuxer.MuxerPathOrDefault(),
                        WorkingDirectory = projectDir,
                        Arguments = new[]
                        {
                            "msbuild",
                            _projectFile,
                            $"/p:_DotNetWatchListFile={watchList}"
                        }.Concat(_buildFlags),
                        OutputData = a =>
                        {
                        },
                        ErrorData = a =>
                        {
                        }
                    };

                    _logger.LogDebug($"Running MSBuild target '{TargetName}' on '{_projectFile}'");

                    var exitCode = await _processRunner.RunAsync(processSpec, cancellationToken);

                    if (exitCode == 0 && File.Exists(watchList))
                    {
                        var fileset = new FileSet(
                            File.ReadAllLines(watchList)
                                .Select(l => l?.Trim())
                                .Where(l => !string.IsNullOrEmpty(l)));

                        _logger.LogDebug($"Watching {fileset.Count} file(s) for changes");
#if DEBUG

                        foreach (var file in fileset)
                        {
                            _logger.LogDebug($"  -> {file}");
                        }

                        Debug.Assert(fileset.All(Path.IsPathRooted), "All files should be rooted paths");
#endif

                        return fileset;
                    }

                    _logger.LogError($"Error(s) finding watch items project file '{Path.GetFileName(_projectFile)}'");

                    _logger.LogInformation($"MSBuild output from target '{TargetName}':");
                    _logger.LogInformation(string.Empty);

                    _logger.LogInformation(string.Empty);

                    if (!_waitOnError)
                    {
                        return null;
                    }
                    else
                    {
                        _logger.LogWarning("Fix the error to continue or press Ctrl+C to exit.");

                        var fileSet = new FileSet(new[] { _projectFile });

                        using (var watcher = new FileSetWatcher(fileSet, _logger))
                        {
                            await watcher.GetChangedFileAsync(cancellationToken);

                            _logger.LogInformation($"File changed: {_projectFile}");
                        }
                    }
                }
            }
            finally
            {
                if (File.Exists(watchList))
                {
                    File.Delete(watchList);
                }
            }
        }

        private IReadOnlyList<string> InitializeArgs(string watchTargetsFile, bool trace)
        {
            var args = new List<string>
            {
                "/nologo",
                "/v:n",
                "/t:" + TargetName,
                "/p:DotNetWatchBuild=true", // extensibility point for users
                "/p:DesignTimeBuild=true", // don't do expensive things
                "/p:CustomAfterMicrosoftCommonTargets=" + watchTargetsFile,
                "/p:CustomAfterMicrosoftCommonCrossTargetingTargets=" + watchTargetsFile,
            };

            if (trace)
            {
                // enables capturing markers to know which projects have been visited
                args.Add("/p:_DotNetWatchTraceOutput=true");
            }

            return args;
        }

        private string FindTargetsFile()
        {
            var assemblyDir = Path.GetDirectoryName(typeof(MsBuildFileSetFactory).Assembly.Location);
            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Watch", "assets"),
                Path.Combine(assemblyDir, "Watch", "assets"),
                AppContext.BaseDirectory,
                assemblyDir,
            };

            var targetPath = searchPaths.Select(p => Path.Combine(p, WatchTargetsFileName)).FirstOrDefault(File.Exists);
            if (targetPath == null)
            {
                _logger.LogError("Fatal error: could not find DotNetWatch.targets");
                return null;
            }
            return targetPath;
        }
    }
}
