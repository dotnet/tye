// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Hosting.Model
{
    public class IngressRule
    {
        public IngressRule(string? host, string? path, string service)
        {
            Host = host;
            Path = path;
            Service = service;
        }

        public string? Host { get; }
        public string? Path { get; }
        public string Service { get; }
    }
}
