// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigConfigurationSource
    {
        [Required]
        public string Name { get; set; } = default!;

        [Required]
        public string Value { get; set; } = default!;
    }
}
