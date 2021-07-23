namespace Microsoft.Tye.Hosting.Model.V1
{
    public class V1Metric
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public V1MetricMetadata? Metadata { get; set; }
    }
}
