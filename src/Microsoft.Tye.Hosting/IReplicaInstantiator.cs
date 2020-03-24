// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
