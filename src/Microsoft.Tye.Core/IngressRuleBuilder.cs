// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public sealed class IngressRuleBuilder
    {
        public string? Path { get; set; }
        public string? Host { get; set; }
        public string? Service { get; set; }
    }
}
