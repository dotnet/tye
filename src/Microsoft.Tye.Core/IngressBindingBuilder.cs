// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public sealed class IngressBindingBuilder
    {
        public string? Name { get; set; }
        public bool AutoAssignPort { get; set; }
        public int? Port { get; set; }
        public string? Protocol { get; set; } // HTTP or HTTPS
    }
}
