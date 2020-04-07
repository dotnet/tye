// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model
{
    public class EffectiveBinding
    {
        public EffectiveBinding(string service, string? name, string? protocol, string host, int? port, string? connectionString, List<EnvironmentVariable> env)
        {
            Service = service;
            Name = name;
            Protocol = protocol;
            Host = host;
            Port = port;
            ConnectionString = connectionString;
            Env = env;
        }

        public string Service { get; }

        public string? Name { get; }

        public string? Protocol { get; }

        public string Host { get; }

        public int? Port { get; }

        public string? ConnectionString { get; }

        public List<EnvironmentVariable> Env { get; }
    }
}
