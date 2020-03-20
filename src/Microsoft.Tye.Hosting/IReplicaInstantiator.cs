using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public interface IReplicaInstantiator : IApplicationProcessor
    {
        Task HandleStaleReplica(ReplicaEvent replicaEvent);

        ValueTask<IDictionary<string, string>> SerializeReplica(ReplicaEvent replicaEvent);

        ValueTask<ReplicaEvent> DeserializeReplicaEvent(IDictionary<string, string> serializedEvent);
    }
}
