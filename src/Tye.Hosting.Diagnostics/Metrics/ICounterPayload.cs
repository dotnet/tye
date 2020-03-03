using System;
using System.Collections.Generic;
using System.Text;

namespace Tye.Hosting.Diagnostics.Metrics
{
    internal interface ICounterPayload
    {
        public string Name { get; }
        public string Value { get; }
    }
}
