// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model
{
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
}
