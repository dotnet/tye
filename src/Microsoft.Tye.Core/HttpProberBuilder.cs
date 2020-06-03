// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public class HttpProberBuilder
    {
        public string Path { get; set; } = default!;
        public int? Port { get; set; }
        public string? Protocol { get; set; }
        public List<KeyValuePair<string, object>> Headers { get; set; } = new List<KeyValuePair<string, object>>();
    }
}
