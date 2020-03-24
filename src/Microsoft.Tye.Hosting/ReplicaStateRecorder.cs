// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class ReplicaStateRecorder : IApplicationProcessor
    {
        private readonly ILogger _logger;
        private ReplicaRegistry _registry;

        public ReplicaStateRecorder(Model.Application application, ILogger logger, IDictionary<ServiceType, IReplicaInstantiator> replicaInstantiators)
        {
            _logger = logger;
            _registry = new ReplicaRegistry(application, replicaInstantiators);
        }

        public async Task StartAsync(Model.Application application)
        {
            await _registry.Reset();

            foreach (var service in application.Services.Values)
            {
                service.Items[typeof(Subscription)] = service.ReplicaEvents.Subscribe(OnReplicaChanged);
            }
        }

        public Task StopAsync(Model.Application application)
        {
            foreach (var service in application.Services.Values)
            {
                if (service.Items.TryGetValue(typeof(Subscription), out var item) && item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _registry.Dispose();
            return Task.CompletedTask;
        }

        private void OnReplicaChanged(ReplicaEvent replicaEvent)
        {
            if (replicaEvent.State != ReplicaState.Started)
            {
                return;
            }

            _logger.LogDebug("recording replica change event for {name}", replicaEvent.Replica.Name);

            _registry.WriteReplicaEvent(replicaEvent);
        }

        private class Subscription { }
    }
}
