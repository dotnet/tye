// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public sealed class VolumeBuilder
    {
        public VolumeBuilder(string? source, string? name, string target)
        {
            Source = source;
            Name = name;
            Target = target;
        }

        public string? Source { get; }

        public string? Name { get; }

        public string Target { get; }
    }
}
