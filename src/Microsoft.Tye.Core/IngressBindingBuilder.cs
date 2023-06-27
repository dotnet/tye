// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public sealed class IngressBindingBuilder
    {
        public string? Name { get; set; }
        public int? Port { get; set; }
        public string? Protocol { get; set; } // HTTP or HTTPS
        public string? IPAddress { get; set; }

        public override string ToString()
        {
            return (string.IsNullOrEmpty(Name) ? "" : "[" + Name + "] -> ") +
                   (string.IsNullOrEmpty(Protocol) ? "" : Protocol + "://")
                   + (string.IsNullOrEmpty(IPAddress) ? "*" : IPAddress)
                   + (Port == null || Port == 0 ? "" : ":" + Port);
        }
    }
}
