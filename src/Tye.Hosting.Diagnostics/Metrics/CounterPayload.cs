using System;
using System.Collections.Generic;
using System.Text;

namespace Micronetes.Hosting.Diagnostics.Metrics
{
    internal class CounterPayload : ICounterPayload
    {
        public CounterPayload(IDictionary<string, object> payloadFields)
        {
            Name = payloadFields["Name"].ToString();
            Value = payloadFields["Mean"].ToString();
        }

        public string Name { get; }
        public string Value { get; }

        public static ICounterPayload FromPayload(IDictionary<string, object> eventPayload)
        {
            if (eventPayload.ContainsKey("CounterType"))
            {
                return eventPayload["CounterType"].Equals("Sum") ? (ICounterPayload)new IncrementingCounterPayload(eventPayload) : (ICounterPayload)new CounterPayload(eventPayload);
            }

            return eventPayload.Count == 6 ? (ICounterPayload)new IncrementingCounterPayload(eventPayload) : (ICounterPayload)new CounterPayload(eventPayload);
        }
    }
}
