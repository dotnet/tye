using System;

namespace Microsoft.Tye.Hosting.Model
{
    public class Probe
    {
        public HttpProbe? Http { get; set; }
        public TimeSpan InitialDelay { get; set; }
        public TimeSpan Period { get; set; }
    }
}
