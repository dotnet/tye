// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using YamlDotNet.Serialization;

namespace Microsoft.Tye.ConfigModel
{
    // General design note re: nullability - we use [Required] to validate for non-null
    // so we use = default! to reflect that. Code that creates a ConfigApplication should
    // validate it before deferencing the properties.
    public class ConfigApplication
    {
        // This gets set by all of the code paths that read the application
        [YamlIgnore]
        public FileInfo Source { get; set; } = default!;

        public string? Name { get; set; }

        public string? Registry { get; set; }

        public List<ConfigService> Services { get; set; } = new List<ConfigService>();

        public List<ConfigIngress> Ingress { get; set; } = new List<ConfigIngress>();
    }
}
