// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Tye.ConfigModel;

namespace Microsoft.Tye
{
    public class ApplicationFactoryFilter
    {
        public Func<ConfigService, bool>? ServicesFilter { get; set; }
        public Func<ConfigIngress, bool>? IngressFilter { get; set; }
    }
}
