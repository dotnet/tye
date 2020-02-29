using System;

namespace Opulence
{
    public sealed class ContainerRegistry
    {
        public ContainerRegistry(string hostname)
        {
            if (hostname is null)
            {
                throw new ArgumentNullException(nameof(hostname));
            }

            Hostname = hostname;
        }

        public string Hostname { get; }
    }
}