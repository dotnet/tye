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

namespace Microsoft.Tye.Hosting
{
    public class ReplicaRegistry : IDisposable
    {
        private readonly IDictionary<ServiceType, IReplicaInstantiator> _replicaInstantiators;
        private readonly StateStore _store;

        public ReplicaRegistry(Model.Application application, ILogger logger, IDictionary<ServiceType, IReplicaInstantiator> replicaInstantiators)
        {
            _replicaInstantiators = replicaInstantiators;
            _store = new StateStore(application, logger);
        }

        public async Task Reset()
        {
            await PurgeAll();
            _store.Reset(delete: true, create: true);
        }

        public async Task Delete()
        {
            await PurgeAll();
            _store.Reset(delete: true, create: false);
        }

        public void WriteReplicaEvent(ReplicaEvent replicaEvent)
        {
            var serviceType = replicaEvent.Replica.Service.ServiceType;
            if (_replicaInstantiators.TryGetValue(serviceType, out var instantiator))
            {
                var serialized = instantiator.SerializeReplica(replicaEvent);
                _store.WriteEvent(new StoreEvent
                {
                    ServiceType = serviceType,
                    State = replicaEvent.State,
                    SerializedEvent = serialized
                });
            }
        }

        private async Task PurgeAll()
        {
            var events = await _store.GetEvents();
            var tasks = new List<Task>(events.Count);

            foreach (var @event in events)
            {
                if (_replicaInstantiators.TryGetValue(@event.ServiceType, out var instantiator))
                {
                    var replica = instantiator.DeserializeReplicaEvent(@event.SerializedEvent);
                    tasks.Add(instantiator.HandleStaleReplica(replica));
                }
            }

            await Task.WhenAll(tasks);
        }

        public void Dispose()
        {
            _store.Dispose();
        }

        private struct StoreEvent
        {
            public ServiceType ServiceType { get; set; }
            public ReplicaState State { get; set; }
            public IDictionary<string, string?> SerializedEvent { get; set; }
        }

        private class StateStore : IDisposable
        {
            //TOOD: Consider Sqlite...

            private ILogger _logger;

            private string _tyeFolderPath;
            private string _eventsFile;

            public StateStore(Model.Application application, ILogger logger)
            {
                _logger = logger;
                _tyeFolderPath = Path.Join(Path.GetDirectoryName(application.Source), ".tye");
                Reset(delete: false, create: true);

                _eventsFile = Path.Join(_tyeFolderPath, "events");
            }

            public bool WriteEvent(StoreEvent @event)
            {
                try
                {
                    var contents = JsonSerializer.Serialize(@event, options: new JsonSerializerOptions {WriteIndented = false});
                    File.AppendAllText(_eventsFile, contents + Environment.NewLine);

                    return true;
                }
                catch (DirectoryNotFoundException ex)
                {
                    _logger.LogWarning(ex, "tye folder is not found. file: {file}", _eventsFile);
                    return false;
                }
            }

            public async ValueTask<IList<StoreEvent>> GetEvents()
            {
                if (!File.Exists(_eventsFile))
                {
                    return Array.Empty<StoreEvent>();
                }

                var contents = await File.ReadAllTextAsync(_eventsFile);
                var events = contents.Split(Environment.NewLine);

                return events.Where(e => !string.IsNullOrEmpty(e.Trim()))
                    .Select(e => JsonSerializer.Deserialize<StoreEvent>(e))
                    .ToList();
            }

            public void Reset(bool delete, bool create)
            {
                if (delete && Directory.Exists(_tyeFolderPath))
                {
                    Directory.Delete(_tyeFolderPath, true);
                }

                if (create)
                {
                    Directory.CreateDirectory(_tyeFolderPath);
                }
            }

            public void Dispose()
            {
                Reset(delete: true, create: false);
            }
        }
    }
}
