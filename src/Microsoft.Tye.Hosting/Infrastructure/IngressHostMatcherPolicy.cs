// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Routing.Matching
{
    internal sealed class IngressHostMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        private const string WildcardHost = "*";
        private const string WildcardPrefix = "*.";

        // Run after HTTP methods, but before 'default'.
        public override int Order { get; } = -100;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            return endpoints.Any(e =>
            {
                var hosts = e.Metadata.GetMetadata<IngressHostMetadata>()?.Hosts;
                if (hosts == null || hosts.Count == 0)
                {
                    return false;
                }

                foreach (var host in hosts)
                {
                    // Don't run policy on endpoints that match everything
                    var key = CreateEdgeKey(host);
                    if (!key.MatchesAll)
                    {
                        return true;
                    }
                }

                return false;
            });
        }

        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (!candidates.IsValidCandidate(i))
                {
                    continue;
                }

                var hosts = candidates[i].Endpoint.Metadata.GetMetadata<IngressHostMetadata>()?.Hosts;
                if (hosts == null || hosts.Count == 0)
                {
                    // Can match any host.
                    continue;
                }

                var matched = false;
                var (requestHost, requestPort) = GetHostAndPort(httpContext);
                for (var j = 0; j < hosts.Count; j++)
                {
                    var host = hosts[j].AsSpan();
                    var port = ReadOnlySpan<char>.Empty;

                    // Split into host and port
                    var pivot = host.IndexOf(':');
                    if (pivot >= 0)
                    {
                        port = host.Slice(pivot + 1);
                        host = host.Slice(0, pivot);
                    }

                    if (host == null || MemoryExtensions.Equals(host, WildcardHost, StringComparison.OrdinalIgnoreCase))
                    {
                        // Can match any host
                    }
                    else if (
                        host.StartsWith(WildcardPrefix) &&

                        // Note that we only slice of the `*`. We want to match the leading `.` also.
                        MemoryExtensions.EndsWith(requestHost, host.Slice(WildcardHost.Length), StringComparison.OrdinalIgnoreCase))
                    {
                        // Matches a suffix wildcard.
                    }
                    else if (MemoryExtensions.Equals(requestHost, host, StringComparison.OrdinalIgnoreCase))
                    {
                        // Matches exactly
                    }
                    else
                    {
                        // If we get here then the host doesn't match.
                        continue;
                    }

                    if (MemoryExtensions.Equals(port, WildcardHost, StringComparison.OrdinalIgnoreCase))
                    {
                        // Port is a wildcard, we allow any port.
                    }
                    else if (port.Length > 0 && (!int.TryParse(port, out var parsed) || parsed != requestPort))
                    {
                        // If we get here then the port doesn't match.
                        continue;
                    }

                    matched = true;
                    break;
                }

                if (!matched)
                {
                    candidates.SetValidity(i, false);
                }
            }

            return Task.CompletedTask;
        }

        private static EdgeKey CreateEdgeKey(string host)
        {
            if (host == null)
            {
                return EdgeKey.WildcardEdgeKey;
            }

            var hostParts = host.Split(':');
            if (hostParts.Length == 1)
            {
                if (!string.IsNullOrEmpty(hostParts[0]))
                {
                    return new EdgeKey(hostParts[0], null);
                }
            }
            if (hostParts.Length == 2)
            {
                if (!string.IsNullOrEmpty(hostParts[0]))
                {
                    if (int.TryParse(hostParts[1], out var port))
                    {
                        return new EdgeKey(hostParts[0], port);
                    }
                    else if (string.Equals(hostParts[1], WildcardHost, StringComparison.Ordinal))
                    {
                        return new EdgeKey(hostParts[0], null);
                    }
                }
            }

            throw new InvalidOperationException($"Could not parse host: {host}");
        }

        private static (string host, int? port) GetHostAndPort(HttpContext httpContext)
        {
            var hostString = httpContext.Request.Host;
            if (hostString.Port != null)
            {
                return (hostString.Host, hostString.Port);
            }
            else if (string.Equals("https", httpContext.Request.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return (hostString.Host, 443);
            }
            else if (string.Equals("http", httpContext.Request.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return (hostString.Host, 80);
            }
            else
            {
                return (hostString.Host, null);
            }
        }

        private readonly struct EdgeKey
        {
            internal static readonly EdgeKey WildcardEdgeKey = new EdgeKey(null, null);

            public readonly int? Port;
            public readonly string Host;

            public EdgeKey(string? host, int? port)
            {
                Host = host ?? WildcardHost;
                Port = port;

                HasHostWildcard = Host.StartsWith(WildcardPrefix, StringComparison.Ordinal);
            }

            public bool HasHostWildcard { get; }

            public bool MatchesHost => !string.Equals(Host, WildcardHost, StringComparison.Ordinal);

            public bool MatchesPort => Port != null;

            public bool MatchesAll => !MatchesHost && !MatchesPort;
        }
    }
}
