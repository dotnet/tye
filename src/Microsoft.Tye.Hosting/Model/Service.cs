// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

namespace Microsoft.Tye.Hosting.Model
{
    public class Service
    {
        public Service(ServiceDescription description, ServiceSource source)
        {
            Description = description;

            Logs.Subscribe(entry =>
            {
                if (CachedLogs.Count > 5000)
                {
                    CachedLogs.TryDequeue(out _);
                }

                CachedLogs.Enqueue(entry);
            });

            ReplicaEvents.Subscribe(entry =>
            {
                entry.Replica.State = entry.State;
            });

            ServiceSource = source;
        }

        public ServiceDescription Description { get; }

        public int Restarts { get; set; }

        public ServiceSource ServiceSource { get; }

        public ServiceType ServiceType
        {
            get
            {
                if (Description.RunInfo is DockerRunInfo)
                {
                    return ServiceType.Container;
                }

                if (Description.RunInfo is ExecutableRunInfo)
                {
                    return ServiceType.Executable;
                }

                if (Description.RunInfo is ProjectRunInfo)
                {
                    return ServiceType.Project;
                }

                if (Description.RunInfo is IngressRunInfo)
                {
                    return ServiceType.Ingress;
                }

                if (Description.RunInfo is AzureFunctionRunInfo)
                {
                    return ServiceType.Function;
                }

                return ServiceType.External;
            }
        }

        public ServiceStatus Status { get; set; } = new ServiceStatus();

        public ConcurrentDictionary<string, ReplicaStatus> Replicas { get; set; } = new ConcurrentDictionary<string, ReplicaStatus>();

        public Dictionary<object, object> Items { get; } = new Dictionary<object, object>();

        public ConcurrentQueue<string> CachedLogs { get; } = new ConcurrentQueue<string>();

        public Subject<string> Logs { get; } = new Subject<string>();

        public Subject<ReplicaEvent> ReplicaEvents { get; } = new Subject<ReplicaEvent>();

        public ServiceState State
        {
            get
            {
                var replicaStates = Replicas.Values.Select(r => r.State);
                int replicaCount = replicaStates.Count();


                if (replicaCount == 0)
                    return ServiceState.Unknown;

                if (replicaStates.Any(r => r == ReplicaState.Added))
                    return ServiceState.Starting;

                if (replicaStates.All(r => r == ReplicaState.Started || r == ReplicaState.Ready || r == ReplicaState.Healthy))
                    return ServiceState.Started;

                if (replicaCount == 1)
                {
                    ReplicaState? replicaState = replicaStates.Single();

                    if (replicaState == ReplicaState.Removed)
                        return ServiceState.Failed;

                    if (replicaState == ReplicaState.Stopped)
                        return ServiceState.Stopped;
                }
                else
                {
                    if (replicaStates.All(r => r == ReplicaState.Stopped))
                        return ServiceState.Stopped;

                    if (replicaStates.Any(r => r == ReplicaState.Removed || r == ReplicaState.Stopped))
                        return ServiceState.Degraded;
                }

                return ServiceState.Unknown;
            }
        }
    }

    public enum ServiceState
    {
        Unknown,
        Starting,
        Started,
        Degraded,
        Failed,
        Stopped
    }
}
