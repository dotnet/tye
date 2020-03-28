// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Model;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Collections.Concurrent;

namespace Microsoft.Tye.Hosting
{
    public class ReplicaRegistry : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileWriteSemaphores;
        private readonly string _tyeFolderPath;

        public ReplicaRegistry(Model.Application application, ILogger logger)
        {
            _logger = logger;
            _fileWriteSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
            _tyeFolderPath = Path.Join(Path.GetDirectoryName(application.Source), ".tye");

            if (!Directory.Exists(_tyeFolderPath))
            {
                Directory.CreateDirectory(_tyeFolderPath);
            }
        }

        public bool WriteReplicaEvent(string storeName, IDictionary<string, string> replicaRecord)
        {
            var filePath = Path.Join(_tyeFolderPath, GetStoreFile(storeName));
            var contents = JsonSerializer.Serialize(replicaRecord, new JsonSerializerOptions { WriteIndented = false });
            var semaphore = GetSempahoreForStore(storeName);

            semaphore.Wait();
            try
            {
                File.AppendAllText(filePath, contents + Environment.NewLine);
                return true;
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogWarning(ex, "tye folder is not found. file: {file}", filePath);
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public bool DeleteStore(string storeName)
        {
            var filePath = Path.Join(_tyeFolderPath, GetStoreFile(storeName));

            try
            {
                File.Delete(storeName);
                return true;
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogWarning(ex, "tye folder is not found. file: {file}", filePath);
                return false;
            }
        }

        public async ValueTask<IList<IDictionary<string, string>>> GetEvents(string storeName)
        {
            var filePath = Path.Join(_tyeFolderPath, GetStoreFile(storeName));
            
            if (!File.Exists(filePath))
            {
                return Array.Empty<IDictionary<string, string>>();
            }

            var contents = await File.ReadAllTextAsync(filePath);
            var events = contents.Split(Environment.NewLine);

            return events.Where(e => !string.IsNullOrEmpty(e.Trim()))
                .Select(e => JsonSerializer.Deserialize<IDictionary<string, string>>(e))
                .ToList();
        }

        private SemaphoreSlim GetSempahoreForStore(string storeName)
        {
            return _fileWriteSemaphores.GetOrAdd(storeName, _ => new SemaphoreSlim(1, 1));
        }

        private string GetStoreFile(string storeName) => $"{storeName}_store";

        public void Dispose()
        {
            Directory.Delete(_tyeFolderPath, true);
        }
    }
}
