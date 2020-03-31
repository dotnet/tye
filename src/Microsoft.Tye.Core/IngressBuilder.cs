// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public sealed class IngressBuilder
    {
        public IngressBuilder(string name)
        {
            Name = name;
        }

        public string Name { get; set; }

        public int Replicas { get; set; } = 1;

        public List<IngressBindingBuilder> Bindings { get; set; } = new List<IngressBindingBuilder>();

        public List<IngressRuleBuilder> Rules { get; set; } = new List<IngressRuleBuilder>();
    }
}
