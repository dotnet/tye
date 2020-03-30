// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigVolume
    {
        public string Source { get; set; } = default!;

        public string Name { get; set; } = default!;

        [Required]
        public string Target { get; set; } = default!;
    }
}
