﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Diagnostics.Metrics
{
    internal class IncrementingCounterPayload : ICounterPayload
    {
        public IncrementingCounterPayload(IDictionary<string, object> payloadFields)
        {
            Name = payloadFields["Name"].ToString()!;
            Value = payloadFields["Increment"].ToString()!;
        }

        public string Name { get; }
        public string Value { get; }
    }
}
