// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watcher.Internal
{
    public class FileWatcher
    {
        private bool _disposed;

        private readonly IDictionary<string, IFileSystemWatcher> _watchers;
        private readonly ILogger _logger;

        public FileWatcher(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _watchers = new Dictionary<string, IFileSystemWatcher>();
        }

        public event Action<string>? OnFileChange;

        public void WatchDirectory(string directory)
        {
            EnsureNotDisposed();
            AddDirectoryWatcher(directory);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var watcher in _watchers)
            {
                watcher.Value.OnFileChange -= WatcherChangedHandler!;
                watcher.Value.OnError -= WatcherErrorHandler!;
                watcher.Value.Dispose();
            }

            _watchers.Clear();
        }

        private void AddDirectoryWatcher(string directory)
        {
            directory = EnsureTrailingSlash(directory);

            var alreadyWatched = _watchers
                .Where(d => directory.StartsWith(d.Key))
                .Any();

            if (alreadyWatched)
            {
                return;
            }

            var redundantWatchers = _watchers
                .Where(d => d.Key.StartsWith(directory))
                .Select(d => d.Key)
                .ToList();

            if (redundantWatchers.Any())
            {
                foreach (var watcher in redundantWatchers)
                {
                    DisposeWatcher(watcher);
                }
            }

            var newWatcher = FileWatcherFactory.CreateWatcher(directory);
            newWatcher.OnFileChange += WatcherChangedHandler!;
            newWatcher.OnError += WatcherErrorHandler!;
            newWatcher.EnableRaisingEvents = true;

            _watchers.Add(directory, newWatcher);
        }

        private void WatcherErrorHandler(object sender, Exception error)
        {
            if (sender is IFileSystemWatcher watcher)
            {
                _logger.LogWarning($"The file watcher observing '{watcher.BasePath}' encountered an error: {error.Message}");
            }
        }

        private void WatcherChangedHandler(object sender, string changedPath)
        {
            NotifyChange(changedPath);
        }

        private void NotifyChange(string path)
        {
            if (OnFileChange != null)
            {
                OnFileChange(path);
            }
        }

        private void DisposeWatcher(string directory)
        {
            var watcher = _watchers[directory];
            _watchers.Remove(directory);

            watcher.EnableRaisingEvents = false;

            watcher.OnFileChange -= WatcherChangedHandler!;
            watcher.OnError -= WatcherErrorHandler!;

            watcher.Dispose();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FileWatcher));
            }
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (!string.IsNullOrEmpty(path) &&
                path[path.Length - 1] != Path.DirectorySeparatorChar)
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }
    }
}
