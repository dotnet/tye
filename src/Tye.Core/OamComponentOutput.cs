﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using YamlDotNet.RepresentationModel;

namespace Tye
{
    internal sealed class OamComponentOutput : ServiceOutput, IYamlManifestOutput
    {
        public OamComponentOutput(string name, YamlDocument yaml)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (yaml is null)
            {
                throw new ArgumentNullException(nameof(yaml));
            }

            Name = name;
            Yaml = yaml;
        }

        public string Name { get; }
        public YamlDocument Yaml { get; }
    }
}
