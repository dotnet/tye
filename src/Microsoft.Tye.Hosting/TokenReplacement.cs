// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    internal static class TokenReplacement
    {
        public static string ReplaceEnvironmentValues(string text, List<EffectiveBinding> bindings)
        {
            var tokens = GetTokens(text);
            foreach (var token in tokens)
            {
                var selectedBinding = ResolveEnvironmentToken(token, bindings);
                if (selectedBinding is null)
                {
                    throw new InvalidOperationException($"No available substitutions found for token '{token}'.");
                }
                
                if (selectedBinding.Value.text == null) continue;
                text = selectedBinding.Value.text;

                var selectedBindingtokens = GetTokens(selectedBinding.Value.text);
                foreach (var bindingtoken in selectedBindingtokens)
                {
                    var replacement = ReplaceValues(bindingtoken, selectedBinding.Value.binding, bindings);
                    text = text.Replace(bindingtoken, replacement);
                }

            }
            return text;
        }
        
        public static string ReplaceValues(string text, EffectiveBinding binding, List<EffectiveBinding> bindings)
        {
            var tokens = GetTokens(text);
            foreach (var token in tokens)
            {
                var replacement = ResolveToken(token, binding, bindings);
                if (replacement is null)
                {
                    throw new InvalidOperationException($"No available substitutions found for token '{token}'.");
                }

                text = text.Replace(token, replacement);
            }

            return text;
        }

        private static HashSet<string> GetTokens(string text)
        {
            var tokens = new HashSet<string>(StringComparer.Ordinal);

            var i = 0;
            while ((i = text.IndexOf("${", i)) != -1)
            {
                var start = i;
                var end = (int?)null;
                for (; i < text.Length; i++)
                {
                    if (text[i] == '}')
                    {
                        end = i;
                        break;
                    }
                }

                if (end is null)
                {
                    throw new FormatException($"Value '{text}' contains an unclosed replacement token '{text[start..text.Length]}'.");
                }

                var token = text[start..(end.Value + 1)];
                tokens.Add(token);
            }

            return tokens;
        }
        
        private static (EffectiveBinding binding, string? text)? ResolveEnvironmentToken(string token, List<EffectiveBinding> bindings)
        {
            // The language we support for tokens is meant to be pretty DRY. It supports a few different formats:
            //
            // - ${host}: only allowed inside a connection string, it can refer to the binding.
            // - ${env:SOME_VAR}: allowed anywhere. It can refer to any environment variable defined for *this* service.
            // - ${service:myservice:port}: allowed anywhere. It can refer to the protocol/host/port of bindings.
           
            var keys = token[2..^1].Split(':');
            if (keys.Length == 3 && keys[0] == "service")
            {
                var binding = bindings.FirstOrDefault(b => b.Service == keys[1] && b.Name == null)!;
                return (binding, GetValueFromBinding(binding, keys[2]));
            }
            else if (keys.Length == 4 && keys[0] == "service")
            {
                var binding  = bindings.FirstOrDefault(b => b.Service == keys[1] && b.Name == keys[2])!;
                return (binding, GetValueFromBinding(binding, keys[3]));
            }

            return null;

            string? GetValueFromBinding(EffectiveBinding binding, string key)
            {
                return key switch
                {
                    "protocol" => binding.Protocol,
                    "host" => binding.Host,
                    "port" => binding.Port?.ToString(CultureInfo.InvariantCulture),
                    "connectionString" => binding.ConnectionString,
                    _ => null,
                };
            }
        }

        private static string? ResolveToken(string token, EffectiveBinding binding, List<EffectiveBinding> bindings)
        {
            // The language we support for tokens is meant to be pretty DRY. It supports a few different formats:
            //
            // - ${host}: only allowed inside a connection string, it can refer to the binding.
            // - ${env:SOME_VAR}: allowed anywhere. It can refer to any environment variable defined for *this* service.
            // - ${service:myservice:port}: allowed anywhere. It can refer to the protocol/host/port of bindings.

            var keys = token[2..^1].Split(':');
            if (keys.Length == 1)
            {
                // If there's a single key, it has to be the simple format.
                return GetValueFromBinding(binding, keys[0]);
            }
            else if (keys.Length == 2 && keys[0] == "env")
            {
                return GetEnvironmentVariable(binding, keys[1]);
            }
            else if (keys.Length == 3 && keys[0] == "service")
            {
                binding = bindings.FirstOrDefault(b => b.Service == keys[1] && b.Name == null)!;
                return GetValueFromBinding(binding, keys[2]);
            }
            else if (keys.Length == 4 && keys[0] == "service")
            {
                binding = bindings.FirstOrDefault(b => b.Service == keys[1] && b.Name == keys[2])!;
                return GetValueFromBinding(binding, keys[3]);
            }

            return null;

            string? GetValueFromBinding(EffectiveBinding binding, string key)
            {
                return key switch
                {
                    "protocol" => binding.Protocol,
                    "host" => binding.Host,
                    "port" => binding.Port?.ToString(CultureInfo.InvariantCulture),
                    _ => null,
                };
            }

            static string? GetEnvironmentVariable(EffectiveBinding binding, string key)
            {
                var envVar = binding.Env.FirstOrDefault(e => string.Equals(e.Name, key, StringComparison.OrdinalIgnoreCase));
                return envVar?.Value;
            }
        }
    }
}
