﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Tye.ConfigModel
{
    internal class ConfigService
    {
        [Required]
        public string Name { get; set; } = default!;
        public bool External { get; set; }
        public string? DockerImage { get; set; }
        public string? Project { get; set; }
        public bool? Build { get; set; }
        public string? Executable { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? Args { get; set; }
        public int? Replicas { get; set; }
        public List<ConfigServiceBinding> Bindings { get; set; } = new List<ConfigServiceBinding>();

        [YamlMember(Alias = "env")]
        public List<ConfigConfigurationSource> Configuration { get; set; } = new List<ConfigConfigurationSource>();
    }
}
