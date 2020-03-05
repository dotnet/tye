// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tye.Hosting.Model
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
                    CachedLogs.Dequeue();
                }

                CachedLogs.Enqueue(entry);
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

                return ServiceType.External;
            }
        }

        public ServiceStatus Status { get; set; } = new ServiceStatus();

        public ConcurrentDictionary<string, ReplicaStatus> Replicas { get; set; } = new ConcurrentDictionary<string, ReplicaStatus>();

        [JsonIgnore]
        public Dictionary<int, List<int>> PortMap { get; set; } = new Dictionary<int, List<int>>();

        [JsonIgnore]
        public Dictionary<object, object> Items { get; } = new Dictionary<object, object>();

        [JsonIgnore]
        public Queue<string> CachedLogs { get; } = new Queue<string>();

        [JsonIgnore]
        public Subject<string> Logs { get; } = new Subject<string>();

        [JsonIgnore]
        public Subject<ReplicaEvent> ReplicaEvents { get; } = new Subject<ReplicaEvent>();
    }

    public readonly struct ReplicaEvent
    {
        public ReplicaState State { get; }
        public ReplicaStatus Replica { get; }

        public ReplicaEvent(ReplicaState state, ReplicaStatus replica)
        {
            State = state;
            Replica = replica;
        }
    }

    public enum ReplicaState
    {
        Removed,
        Added,
        Started,
        Stopped,
    }

    public class ServiceStatus
    {
        public string? ProjectFilePath { get; set; }
        public string? ExecutablePath { get; set; }
        public string? Args { get; set; }
        public string? WorkingDirectory { get; set; }
    }

    public class ProcessStatus : ReplicaStatus
    {
        public ProcessStatus(Service service, string name)
            : base(service, name)
        {
        }
        public int? ExitCode { get; set; }
        public int? Pid { get; set; }
        public IDictionary<string, string>? Environment { get; set; }
    }

    public class DockerStatus : ReplicaStatus
    {
        public DockerStatus(Service service, string name) : base(service, name)
        {
        }

        public string? DockerCommand { get; set; }

        public string? ContainerId { get; set; }

        public int? DockerLogsPid { get; set; }
    }

    public class ReplicaStatus
    {
        public ReplicaStatus(Service service, string name)
        {
            Service = service;
            Name = name;
        }

        public string Name { get; }

        public static JsonConverter<ReplicaStatus> JsonConverter = new Converter();

        public IEnumerable<int>? Ports { get; set; }

        [JsonIgnore]
        public Service Service { get; }

        [JsonIgnore]
        public Dictionary<object, object> Items { get; } = new Dictionary<object, object>();

        [JsonIgnore]
        public Dictionary<string, string> Metrics { get; set; } = new Dictionary<string, string>();

        private class Converter : JsonConverter<ReplicaStatus>
        {
            public override ReplicaStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, ReplicaStatus value, JsonSerializerOptions options)
            {
                // Use the runtime type since we really want to serialize either the DockerStatus or ProcessStatus
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }
    }

    public enum ServiceType
    {
        External,
        Project,
        Executable,
        Container
    }

    public class PortMapping
    {
        public int ExternalPort { get; set; }

        public List<int> InteralPorts { get; set; } = new List<int>();
    }
}
