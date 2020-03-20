using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Hosting.Model
{
    public class ServiceJson
    {
        public ServiceDescriptionJson? Description { get; set; }
        public ServiceType ServiceType { get; set; }
        public int Restarts { get; set; }
        public ServiceStatus? Status { get; set; }
        public Dictionary<string, ReplicaStatusJson>? Replicas { get; set; }
    }
}
