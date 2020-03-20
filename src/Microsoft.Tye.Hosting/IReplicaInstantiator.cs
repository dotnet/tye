using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public interface IReplicaInstantiator : IApplicationProcessor
    {
        Task HandleStaleReplica(ReplicaEvent replicaEvent);

        ValueTask<string> SerializeReplica(ReplicaEvent replicaEvent);

        ValueTask<ReplicaEvent> DeserializeReplicaEvent(string serializedEvent);
    }
}
