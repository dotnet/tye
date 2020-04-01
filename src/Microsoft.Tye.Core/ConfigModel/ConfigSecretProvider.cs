// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigSecretProvider
    {
        [Required]
        public string Name { get; set; } = default!;

        [Required]
        public string Type { get; set; } = default!;

        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }
}
