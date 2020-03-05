// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Tye
{
    public sealed class Service
    {
        public Service(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
        }

        public string Name { get; }

        public GeneratedAssets GeneratedAssets { get; } = new GeneratedAssets();

        public Source? Source { get; set; }

        public Dictionary<string, object> Environment { get; set; } = new Dictionary<string, object>();

        // Represents bindings *published* by this service
        // See GeneratedAssets for bindings consumed by the service
        public List<ServiceBinding> Bindings { get; } = new List<ServiceBinding>();

        public int Replicas { get; set; }
    }
}
