// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Tye.ConfigModel
{
    public class ConfigServiceBinding
    {
        public string? Name { get; set; }
        public string? ConnectionString { get; set; }
        public int? Port { get; set; }
        public int? InternalPort { get; set; }
        public string? Host { get; set; }
        public string? Protocol { get; set; }
    }
}
