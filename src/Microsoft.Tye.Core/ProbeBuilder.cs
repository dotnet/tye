namespace Microsoft.Tye
{
    public class ProbeBuilder
    {
        public HttpProbeBuilder? Http { get; set; }
        public int InitialDelay { get; set; }
        public int Period { get; set; }
    }
}
