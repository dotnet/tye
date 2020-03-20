using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class ReplicaRegistry : IDisposable
    {
        private readonly IDictionary<ServiceType, IReplicaInstantiator> _replicaInstantiators;
        private StateStore _store;

        public ReplicaRegistry(Model.Application application, IDictionary<ServiceType, IReplicaInstantiator> replicaInstantiators)
        {
            _replicaInstantiators = replicaInstantiators;
            _store = new StateStore(application);
        }

        public async Task Reset()
        {
            await PurgeAll();
            _store.Reset(true, true);
        }

        public async Task WriteReplicaEvent(ReplicaEvent replicaEvent)
        {
            var serviceType = replicaEvent.Replica.Service.ServiceType;
            if (_replicaInstantiators.TryGetValue(serviceType, out var instantiator))
            {
                var serialized = await instantiator.SerializeReplica(replicaEvent);
                await _store.WriteEvent(new StoreEvent
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
                    var replica = await instantiator.DeserializeReplicaEvent(@event.SerializedEvent);
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
            public IDictionary<string, string> SerializedEvent { get; set; }
        }

        private class StateStore : IDisposable
        {
            //TOOD: Consider Sqlite...

            private string _tyeFolderPath;
            private string _eventsFile;

            public StateStore(Model.Application application)
            {
                _tyeFolderPath = Path.Join(Path.GetDirectoryName(application.Source), ".tye");
                Reset(false, true);

                _eventsFile = Path.Join(_tyeFolderPath, "events");
            }

            public async Task WriteEvent(StoreEvent @event)
            {
                var contents = JsonSerializer.Serialize(@event, options: new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                await File.AppendAllTextAsync(_eventsFile, contents + Environment.NewLine);
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
                    Directory.Delete(_tyeFolderPath, true);

                if (create)
                    Directory.CreateDirectory(_tyeFolderPath);
            }

            public void Dispose()
            {
                Reset(true, false);
            }
        }
    }
}
