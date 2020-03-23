// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigIngress
    {
        public string Name { get; set; } = default!;
        public int? Replicas { get; set; }
        public List<ConfigIngressRule> Rules { get; set; } = new List<ConfigIngressRule>();
        public List<ConfigIngressServiceBinding> Bindings { get; set; } = new List<ConfigIngressServiceBinding>();
    }
}
