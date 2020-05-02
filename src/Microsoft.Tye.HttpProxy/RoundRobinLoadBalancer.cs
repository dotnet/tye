using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Tye.HttpProxy
{
    public class RoundRobinLoadBalancer : DelegatingHandler
    {
        private readonly ConcurrentDictionary<string, DnsCache> _dnsCache = new ConcurrentDictionary<string, DnsCache>();
        private readonly ILogger _logger;
        public RoundRobinLoadBalancer(ILogger logger, HttpMessageHandler innerHandler) : base(innerHandler)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var host = request.RequestUri.Host;
            if (!_dnsCache.TryGetValue(host, out var cache))
            {
                var addresses = await Dns.GetHostAddressesAsync(host);

                _logger.LogInformation("Resolved {Host} to {Addresses}", host, addresses);

                cache = new DnsCache(addresses);
                _dnsCache[host] = cache;
            }

            // Allocations!
            request.RequestUri = new UriBuilder(request.RequestUri)
            {
                Host = cache.GetAddress()
            }.Uri;

            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.InnerException is SocketException)
            {
                // Connection error, remove this host (the target might have died)
                _dnsCache.TryRemove(host, out _);

                throw;
            }

        }

        private class DnsCache
        {
            private int _index;
            private readonly string[] _addresses;

            public DnsCache(IPAddress[] addresses)
            {
                _addresses = addresses.Select(a => a.ToString()).ToArray();
            }

            public string GetAddress()
            {
                var next = Interlocked.Increment(ref _index) % _addresses.Length;
                return _addresses[next];
            }
        }
    }
}
