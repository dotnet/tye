using System;

namespace Microsoft.Tye.Hosting.Model
{
    public class Probe
    {
        public HttpProber? Http { get; set; }
        public TimeSpan InitialDelay { get; set; }
        public TimeSpan Period { get; set; }
        public TimeSpan Timeout { get; set; }
        public int SuccessThreshold { get; set; }
        public int FailureThreshold { get; set; }
    }
}
