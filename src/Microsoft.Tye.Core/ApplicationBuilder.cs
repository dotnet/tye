// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Tye
{
    public sealed class ApplicationBuilder
    {
        public ApplicationBuilder(FileInfo source, string name)
        {
            Source = source;
            Name = name;
        }

        public FileInfo Source { get; set; }

        public string Name { get; set; }

        public ContainerRegistry? Registry { get; set; }

        public List<ServiceBuilder> Services { get; } = new List<ServiceBuilder>();

        public List<IngressBuilder> Ingress { get; } = new List<IngressBuilder>();
    }
}
