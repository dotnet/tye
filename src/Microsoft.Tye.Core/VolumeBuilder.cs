// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public sealed class VolumeBuilder
    {
        public VolumeBuilder(string source, string target)
        {
            Source = source;
            Target = target;
        }

        public string Source { get; set; }

        public string Target { get; set; }
    }
}
