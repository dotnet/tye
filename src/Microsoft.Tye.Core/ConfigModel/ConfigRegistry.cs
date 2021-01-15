// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigRegistry
    {
        [Required]
        public string Hostname { get; set; } = null!;

        public string? PullSecret { get; set; }
    }
}
