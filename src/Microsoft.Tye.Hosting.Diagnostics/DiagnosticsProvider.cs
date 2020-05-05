// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Tye
{
    public class DiagnosticsProvider
    {
        public static readonly IReadOnlyDictionary<string, WellKnownProvider> WellKnownProviders = new Dictionary<string, WellKnownProvider>()
        {
            { "ai", new WellKnownProvider("ai", ProviderKind.Logging, "logs: Using ApplicationInsights instrumentation key {InstrumentationKey}") },
            { "elastic", new WellKnownProvider("elastic", ProviderKind.Logging, "logs: Using ElasticSearch at {URL}") },
            { "console", new WellKnownProvider("console", ProviderKind.Logging, "logs: Using console logs") },
            { "seq", new WellKnownProvider("seq", ProviderKind.Logging, "logs: Using Seq at {URL}") },

            { "zipkin", new WellKnownProvider("zipkin", ProviderKind.Tracing, "dtrace: Using Zipkin at URL {URL}") },
        };

        public static bool TryParse(string text, [MaybeNullWhen(false)] out DiagnosticsProvider provider)
        {
            if (string.IsNullOrEmpty(text))
            {
                provider = null!;
                return false;
            }

            var pair = text.Split('=');
            if (pair.Length < 2)
            {
                provider = new DiagnosticsProvider(pair[0].Trim().ToLowerInvariant(), null);
                return true;
            }

            provider = new DiagnosticsProvider(pair[0].Trim().ToLowerInvariant(), pair[1].Trim().ToLowerInvariant());
            return true;
        }

        public DiagnosticsProvider(string key, string? value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; }

        public bool HasValue => !string.IsNullOrEmpty(Value);

        public string? Value { get; }

        public enum ProviderKind
        {
            Logging,
            Metrics,
            Tracing,
            Unknown,
        }

        public class WellKnownProvider
        {
            public WellKnownProvider(string key, ProviderKind kind, string logFormat)
            {
                Key = key;
                Kind = kind;
                LogFormat = logFormat;
            }

            public string Key { get; }

            public ProviderKind Kind { get; }

            public string LogFormat { get; }
        }
    }
}
