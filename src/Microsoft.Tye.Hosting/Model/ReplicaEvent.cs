// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye.Hosting.Model
{
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
}
