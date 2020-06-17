// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace Microsoft.Tye.Hosting.Model
{
    public class Service
    {
        public Service(ServiceDescription description)
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
        }

        public ServiceDescription Description { get; }

        public int Restarts { get; set; }

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

                if (Description.RunInfo is FunctionRunInfo)
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
    }
}
