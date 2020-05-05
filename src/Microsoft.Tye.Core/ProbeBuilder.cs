namespace Microsoft.Tye
{
    public class ProbeBuilder
    {
        public HttpProberBuilder? Http { get; set; }
        public int InitialDelay { get; set; }
        public int Period { get; set; }
        public int Timeout { get; set; }
        public int SuccessThreshold { get; set; }
        public int FailureThreshold { get; set; }
    }
}
