// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigIngress
    {
        [Required]
        public string Name { get; set; } = default!;
        public int? Replicas { get; set; }
        public List<ConfigIngressRule> Rules { get; set; } = new List<ConfigIngressRule>();
        public List<ConfigIngressBinding> Bindings { get; set; } = new List<ConfigIngressBinding>();
    }
}
