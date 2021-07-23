using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model.V1
{
    public class V1ServiceMetrics
    {
        public string? Service { get; set; }
        public List<V1Metric>? Metrics { get; set; }
    }
}
