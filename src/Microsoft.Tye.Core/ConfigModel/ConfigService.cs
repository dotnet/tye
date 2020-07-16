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
        const string ErrorMessage = "A service name must consist of lower case alphanumeric characters or '-'," +
            " start with an alphabetic character, and end with an alphanumeric character" +
            " (e.g. 'my-name',  or 'abc-123', regex used for validation is '[a-z]([-a-z0-9]*[a-z0-9])?').";
        const string MaxLengthErrorMessage = "Name cannot be more that 63 characters long.";

        [Required]
        [RegularExpression("[a-z]([-a-z0-9]*[a-z0-9])?", ErrorMessage = ErrorMessage)]
        [MaxLength(63, ErrorMessage = MaxLengthErrorMessage)]
        public string Name { get; set; } = default!;
        public bool External { get; set; }
        public string? Image { get; set; }
        public string? DockerFile { get; set; }
        public Dictionary<string, string> DockerFileArgs { get; set; } = new Dictionary<string, string>();
        public string? DockerFileContext { get; set; }
        public string? Project { get; set; }
        public string? Include { get; set; }
        public string? Repository { get; set; }
        public bool? Build { get; set; }
        public string? Executable { get; set; }
        public string? WorkingDirectory { get; set; }
        public string? Args { get; set; }
        public int? Replicas { get; set; }
        public List<ConfigServiceBinding> Bindings { get; set; } = new List<ConfigServiceBinding>();
        public List<ConfigVolume> Volumes { get; set; } = new List<ConfigVolume>();
        [YamlMember(Alias = "env")]
        public List<ConfigConfigurationSource> Configuration { get; set; } = new List<ConfigConfigurationSource>();
        public List<BuildProperty> BuildProperties { get; set; } = new List<BuildProperty>();
        public List<string> Tags { get; set; } = new List<string>();
        public ConfigProbe? Liveness { get; set; }
        public ConfigProbe? Readiness { get; set; }
        public string? AzureFunction { get; set; }
        public string? FuncExecutable { get; set; }
        public string? Version { get; set; }
        public string? Architecture { get; set; }
    }
}
