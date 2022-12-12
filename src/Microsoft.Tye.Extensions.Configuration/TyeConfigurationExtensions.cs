﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Sockets;

namespace Microsoft.Extensions.Configuration
{
    public static class TyeConfigurationExtensions
    {
        public static Uri? GetServiceUri(this IConfiguration configuration, string name, string? binding = null)
        {
            var key = GetKey(name, binding);

            var host = configuration[$"service:{key}:host"];
            var port = configuration[$"service:{key}:port"];
            var protocol = configuration[$"service:{key}:protocol"] ?? "http";

            if (string.IsNullOrEmpty(host) || port == null)
            {
                return null;
            }

            if (IPAddress.TryParse(host, out IPAddress address) && address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                host = "[" + host + "]";
            }

            return new Uri(protocol + "://" + host + ":" + port + "/");
        }

        public static string? GetConnectionString(this IConfiguration configuration, string name, string? binding)
        {
            if (binding == null)
            {
                return configuration.GetConnectionString(name);
            }

            var key = GetKey(name, binding);
            var connectionString = configuration[$"connectionstrings:{key}"];
            return connectionString;
        }

        private static string GetKey(string name, string? binding)
        {
            return binding == null ? name : $"{name}:{binding}";
        }
    }
}
