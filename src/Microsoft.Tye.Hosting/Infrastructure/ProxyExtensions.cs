// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Proxy
{
    internal static class ProxyAdvancedExtensions
    {
        private static readonly string[] NotForwardedWebSocketHeaders = new[] { "Connection", "Host", "Upgrade", "Sec-WebSocket-Accept", "Sec-WebSocket-Protocol", "Sec-WebSocket-Key", "Sec-WebSocket-Version", "Sec-WebSocket-Extensions", "Via", "X-Forwarded-For", "X-Forwarded-Proto", "X-Forwarded-Host" };
        private const int DefaultWebSocketBufferSize = 4096;
        private const int StreamCopyBufferSize = 81920;

        public static async Task ProxyRequest(this HttpContext context, HttpMessageInvoker invoker, Uri destinationUri)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (destinationUri == null)
            {
                throw new ArgumentNullException(nameof(destinationUri));
            }

            if (context.WebSockets.IsWebSocketRequest)
            {
                await context.AcceptProxyWebSocketRequest(destinationUri.ToWebSocketScheme());
            }
            else
            {
                using var requestMessage = context.CreateProxyHttpRequest(destinationUri);
                using var responseMessage = await context.SendProxyHttpRequest(invoker, requestMessage);

                await context.CopyProxyHttpResponse(responseMessage);
            }
        }

        public static Uri ToWebSocketScheme(this Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            var uriBuilder = new UriBuilder(uri);
            if (string.Equals(uriBuilder.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                uriBuilder.Scheme = "wss";
            }
            else if (string.Equals(uriBuilder.Scheme, "http", StringComparison.OrdinalIgnoreCase))
            {
                uriBuilder.Scheme = "ws";
            }

            return uriBuilder.Uri;
        }

        public static HttpRequestMessage CreateProxyHttpRequest(this HttpContext context, Uri uri)
        {
            var request = context.Request;

            var requestMessage = new HttpRequestMessage();
            var requestMethod = request.Method;
            if (!HttpMethods.IsGet(requestMethod) &&
                !HttpMethods.IsHead(requestMethod) &&
                !HttpMethods.IsDelete(requestMethod) &&
                !HttpMethods.IsTrace(requestMethod))
            {
                var streamContent = new StreamContent(request.Body);
                requestMessage.Content = streamContent;
            }

            // Copy the request headers
            foreach (var header in request.Headers)
            {
                // taken from : https://github.com/microsoft/reverse-proxy/blob/main/src/ReverseProxy/Forwarder/RequestUtilities.cs#L283
                // HttpClient wrongly uses comma (",") instead of semi-colon (";") as a separator for Cookie headers.
                // To mitigate this, we concatenate them manually and put them back as a single header value.
                // A multi-header cookie header is invalid, but we get one because of
                // https://github.com/dotnet/aspnetcore/issues/26461
                if (string.Equals(header.Key, HeaderNames.Cookie, StringComparison.OrdinalIgnoreCase))
                {
                    requestMessage.Headers.TryAddWithoutValidation(header.Key, string.Join("; ", header.Value.ToArray()));
                }
                else if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                {
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            // Append request forwarding headers
            requestMessage.Headers.TryAddWithoutValidation("Via", $"{context.Request.Protocol} Tye");
            requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Proto", request.Scheme);
            requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Host", request.Host.ToUriComponent());

            if (context.Connection.RemoteIpAddress != null)
            {
                requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-For", context.Connection.RemoteIpAddress.ToString());
            }

            requestMessage.Headers.Host = uri.Authority;
            requestMessage.RequestUri = uri;
            requestMessage.Method = new HttpMethod(request.Method);

            return requestMessage;
        }

        public static async Task<bool> AcceptProxyWebSocketRequest(this HttpContext context, Uri destinationUri)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (destinationUri == null)
            {
                throw new ArgumentNullException(nameof(destinationUri));
            }
            if (!context.WebSockets.IsWebSocketRequest)
            {
                throw new InvalidOperationException();
            }

            using var client = new ClientWebSocket();
            foreach (var protocol in context.WebSockets.WebSocketRequestedProtocols)
            {
                client.Options.AddSubProtocol(protocol);
            }

            foreach (var headerEntry in context.Request.Headers)
            {
                if (!NotForwardedWebSocketHeaders.Contains(headerEntry.Key, StringComparer.OrdinalIgnoreCase))
                {
                    client.Options.SetRequestHeader(headerEntry.Key, headerEntry.Value);
                }
            }

            AppendHeaderValue(client.Options, context.Request.Headers, "Via", context.Request.Protocol);
            AppendHeaderValue(client.Options, context.Request.Headers, "X-Forwarded-Proto", context.Request.Scheme);
            AppendHeaderValue(client.Options, context.Request.Headers, "X-Forwarded-Host", context.Request.Host.ToUriComponent());

            if (context.Connection.RemoteIpAddress != null)
            {
                AppendHeaderValue(client.Options, context.Request.Headers, "X-Forwarded-For", context.Connection.RemoteIpAddress.ToString());
            }

            try
            {
                await client.ConnectAsync(destinationUri, context.RequestAborted);
            }
            catch (WebSocketException)
            {
                context.Response.StatusCode = 400;
                return false;
            }

            using var server = await context.WebSockets.AcceptWebSocketAsync(client.SubProtocol);

            var bufferSize = DefaultWebSocketBufferSize;
            await Task.WhenAll(PumpWebSocket(client, server, bufferSize, context.RequestAborted), PumpWebSocket(server, client, bufferSize, context.RequestAborted));

            return true;

            static void AppendHeaderValue(ClientWebSocketOptions options, IHeaderDictionary headers, string key, string value)
            {
                var newValue = new StringValues(headers[key].Append(value).ToArray());
                options.SetRequestHeader(key, newValue);
            }
        }

        private static async Task PumpWebSocket(WebSocket source, WebSocket destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            var buffer = new byte[bufferSize];
            while (true)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await destination.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, null, cancellationToken);
                    return;
                }
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await destination.CloseOutputAsync(source.CloseStatus!.Value, source.CloseStatusDescription, cancellationToken);
                    return;
                }
                await destination.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, cancellationToken);
            }
        }

        public static Task<HttpResponseMessage> SendProxyHttpRequest(this HttpContext context, HttpMessageInvoker invoker, HttpRequestMessage requestMessage)
        {
            if (requestMessage == null)
            {
                throw new ArgumentNullException(nameof(requestMessage));
            }

            return invoker.SendAsync(requestMessage, context.RequestAborted);
        }

        public static async Task CopyProxyHttpResponse(this HttpContext context, HttpResponseMessage responseMessage)
        {
            if (responseMessage == null)
            {
                throw new ArgumentNullException(nameof(responseMessage));
            }

            var response = context.Response;

            response.StatusCode = (int)responseMessage.StatusCode;
            foreach (var header in responseMessage.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            // SendAsync removes chunking from the response. This removes the header so it doesn't expect a chunked response.
            response.Headers.Remove("transfer-encoding");

            await using var responseStream = await responseMessage.Content.ReadAsStreamAsync();
            await responseStream.CopyToAsync(response.Body, StreamCopyBufferSize, context.RequestAborted);
        }
    }
}
