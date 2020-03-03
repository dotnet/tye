using System;
using System.Collections.Generic;
using System.Text;

namespace Tye.Hosting.Diagnostics.Metrics
{
    internal class IncrementingCounterPayload : ICounterPayload
    {
        public IncrementingCounterPayload(IDictionary<string, object> payloadFields)
        {
            Name = payloadFields["Name"].ToString();
            Value = payloadFields["Increment"].ToString();
        }

        public string Name { get; }
        public string Value { get; }
    }
}
