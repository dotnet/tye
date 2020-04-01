// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigSecrets
    {
        [YamlIgnore]
        public FileInfo Source { get; set; } = default!;

        public string? Name { get; set; }

        public List<ConfigSecretProvider> Providers { get; set; } = new List<ConfigSecretProvider>();
    }
}
