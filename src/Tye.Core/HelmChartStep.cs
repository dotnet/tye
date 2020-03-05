// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Tye
{
    public sealed class HelmChartStep : Step
    {
        public override string DisplayName => "Building Helm Chart...";

        public string ChartName { get; set; } = default!;
    }
}
