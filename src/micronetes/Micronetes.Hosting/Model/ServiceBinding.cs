namespace Micronetes.Hosting.Model
{
    public class ServiceBinding
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public int? Port { get; set; }
        public int? InternalPort { get; set; }
        public string Host { get; set; }
        public string Protocol { get; set; }
    }
}
