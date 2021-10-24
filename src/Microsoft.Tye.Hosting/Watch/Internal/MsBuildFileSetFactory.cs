// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye;

namespace Microsoft.DotNet.Watcher.Internal
{
    public class MsBuildFileSetFactory : IFileSetFactory
    {
        private const string TargetName = "GenerateWatchList";
        private const string WatchTargetsFileName = "DotNetWatch.targets";
        private readonly ILogger _logger;
        private readonly string _projectFile;
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

        internal MsBuildFileSetFactory(ILogger logger,
            string projectFile,
            bool trace)
        {
            _logger = logger;
            _projectFile = projectFile;
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

                    var args = new StringBuilder();
                    args.Append($"msbuild \"{_projectFile}\" /p:_DotNetWatchListFile=\"{watchList}\"");
                    foreach (var flag in _buildFlags)
                    {
                        args.Append(" ");
                        args.Append(flag);
                    }

                    var processSpec = new ProcessSpec
                    {
                        Executable = "dotnet",
                        WorkingDirectory = projectDir!,
                        Arguments = args.ToString()
                    };

                    _logger.LogDebug($"Running MSBuild target '{TargetName}' on '{_projectFile}'");

                    var processResult = await ProcessUtil.RunAsync(processSpec, cancellationToken);

                    if (processResult.ExitCode == 0 && File.Exists(watchList))
                    {
                        var fileset = new FileSet(
                            File.ReadAllLines(watchList)
                                .Select(l => l?.Trim())
                                .Where(l => !string.IsNullOrEmpty(l))!);

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
                        return null!;
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
            string[] searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Watch", "assets"),
                Path.Combine(assemblyDir!, "Watch", "assets"),
                AppContext.BaseDirectory,
                assemblyDir,
            }!;

            var targetPath = searchPaths.Select(p => Path.Combine(p, WatchTargetsFileName)).FirstOrDefault(File.Exists);
            if (targetPath == null)
            {
                _logger.LogError($"Fatal error: could not find {WatchTargetsFileName}");
                return null!;
            }

            return $"\"{targetPath}\"";
        }
    }
}
