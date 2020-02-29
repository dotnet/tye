using System;

namespace Opulence
{
    public class ServiceBinding
    {
        public ServiceBinding(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
        }

        public ServiceBinding(Service service)
        {
            if (service is null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Name = service.Name;

            // We don't copy other properties here because a "hardcoded" value of Protocol/Port is treated differently
            // from just binding the name when considering environments.
        }

        public string Name { get; set; }
        public string? Protocol { get; set; }
        public int? Port { get; set; }
        public Secret? ConnectionString { get; set; }
    }
}