// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace E2ETest
{
    public class RetryHandler : DelegatingHandler
    {
        private static readonly int MaxRetries = 5;
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(500);

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
