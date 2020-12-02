// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    internal sealed class KubernetesIngressOutput : IngressOutput, IYamlManifestOutput
    {
        public KubernetesIngressOutput(string name, YamlDocument yaml)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Yaml = yaml ?? throw new ArgumentNullException(nameof(yaml));
        }

        public string Name { get; }
        public YamlDocument Yaml { get; }
    }
}
