// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigService
    {
        [Required]
        public string Name { get; set; } = default!;
        public bool External { get; set; }
        public string? Image { get; set; }
        public string? Project { get; set; }
        public bool? Build { get; set; }
        public string? Executable { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? Args { get; set; }
        public int? Replicas { get; set; }
        public List<ConfigServiceBinding> Bindings { get; set; } = new List<ConfigServiceBinding>();
        public List<ConfigVolume> Volumes { get; set; } = new List<ConfigVolume>();
        [YamlMember(Alias = "env")]
        public List<ConfigConfigurationSource> Configuration { get; set; } = new List<ConfigConfigurationSource>();
    }
}
