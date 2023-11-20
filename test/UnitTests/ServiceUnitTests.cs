using System.Collections.Generic;
using Microsoft.Tye.Hosting.Model;
using Xunit;

namespace Microsoft.Tye.UnitTests
{
    public class ServiceUnitTests
    {
        [Theory]
        [MemberData(nameof(ServiceStateTestData))]
        public void ServiceStateIsBasedOnReplicaStates(ServiceState expected, List<ReplicaState> replicaStates)
        {
            Service service = new(new ServiceDescription("test", null), ServiceSource.Unknown);

            for (int i = 0; i < replicaStates.Count; i++)
            {
                string replicaName = i.ToString();

                service.Replicas.TryAdd(replicaName, new ReplicaStatus(service, replicaName)
                {
                    State = replicaStates[i],
                });
            }

            Assert.Equal(expected, service.State);

        }

        public static IEnumerable<object[]> ServiceStateTestData =>
            new List<object[]>
            {
                //no replica - should not happen
                new object[] { ServiceState.Unknown, new List<ReplicaState>() },
                
                //one replica
                new object[] { ServiceState.Starting, new List<ReplicaState>() { ReplicaState.Added } },
                new object[] { ServiceState.Started, new List<ReplicaState>() { ReplicaState.Started } },
                new object[] { ServiceState.Started, new List<ReplicaState>() { ReplicaState.Ready } },
                new object[] { ServiceState.Started, new List<ReplicaState>() { ReplicaState.Healthy } },
                new object[] { ServiceState.Failed, new List<ReplicaState>() { ReplicaState.Removed } },
                new object[] { ServiceState.Stopped, new List<ReplicaState>() { ReplicaState.Stopped } },

                //multiple replicas
                new object[] { ServiceState.Starting, new List<ReplicaState>() { ReplicaState.Added, ReplicaState.Started, ReplicaState.Ready, ReplicaState.Healthy } },
                new object[] { ServiceState.Started, new List<ReplicaState>() { ReplicaState.Started, ReplicaState.Ready, ReplicaState.Healthy } },
                new object[] { ServiceState.Degraded, new List<ReplicaState>() { ReplicaState.Removed, ReplicaState.Started, ReplicaState.Ready, ReplicaState.Healthy } },
                new object[] { ServiceState.Degraded, new List<ReplicaState>() { ReplicaState.Stopped, ReplicaState.Started, ReplicaState.Ready, ReplicaState.Healthy } },
                new object[] { ServiceState.Degraded, new List<ReplicaState>() { ReplicaState.Removed, ReplicaState.Stopped, ReplicaState.Started, ReplicaState.Ready, ReplicaState.Healthy } },
                new object[] { ServiceState.Stopped, new List<ReplicaState>() { ReplicaState.Stopped, ReplicaState.Stopped, ReplicaState.Stopped } },
            };
    }
}
