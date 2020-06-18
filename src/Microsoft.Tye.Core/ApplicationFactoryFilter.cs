// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Tye.ConfigModel;

namespace Microsoft.Tye
{
    public class ApplicationFactoryFilter
    {
        public Func<ConfigService, bool>? ServicesFilter { get; set; }
        public Func<ConfigIngress, bool>? IngressFilter { get; set; }

        public static ApplicationFactoryFilter? GetApplicationFactoryFilter(string[] tags)
        {
            ApplicationFactoryFilter? filter = null;

            if (tags != null && tags.Any())
            {
                filter = new ApplicationFactoryFilter
                {
                    ServicesFilter = service => tags.Any(b => service.Tags.Contains(b)),
                    IngressFilter = service => tags.Any(b => service.Tags.Contains(b))
                };
            }

            return filter;
        }
    }
}
