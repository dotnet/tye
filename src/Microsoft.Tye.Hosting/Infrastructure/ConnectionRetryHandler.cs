// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Tye.Hosting
{
    internal class ConnectionRetryHandler : DelegatingHandler
    {
        private static readonly int MaxRetries = 3;
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(1000);

        public ConnectionRetryHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage? response = null;
            var delay = InitialRetryDelay;
            Exception? exception = null;

            for (var i = 0; i < MaxRetries; i++)
            {
                try
                {
                    response = await base.SendAsync(request, cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.InnerException is SocketException)
                {
                    if (i == MaxRetries - 1)
                    {
                        throw;
                    }

                    exception = ex;
                }

                if (response != null &&
                   (response.IsSuccessStatusCode || response.StatusCode != HttpStatusCode.ServiceUnavailable))
                {
                    return response;
                }

                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }

            if (exception != null)
            {
                ExceptionDispatchInfo.Throw(exception);
            }

            throw new TimeoutException();
        }
    }
}
