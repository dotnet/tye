using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace E2ETest
{
    public class RetryHandler : DelegatingHandler
    {
        private static readonly int MaxRetries = 5;
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(100);

        public RetryHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage? response = null;
            var delay = InitialRetryDelay;
            for (var i = 0; i < MaxRetries; i++)
            {
                try
                {
                    response = await base.SendAsync(request, cancellationToken);
                }
                catch (Exception)
                {
                    if (i == MaxRetries - 1)
                    {
                        throw;
                    }
                }

                if (response != null &&
                   (response.IsSuccessStatusCode || response.StatusCode != (HttpStatusCode)503))
                {
                    return response;
                }

                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }

            throw new TimeoutException("Could not reach response after ");
        }
    }
}
