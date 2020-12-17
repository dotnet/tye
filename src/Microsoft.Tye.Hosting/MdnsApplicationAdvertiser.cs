// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Makaretu.Dns;
using Microsoft.AspNetCore.Builder;
using System;
using System.Linq;

namespace Microsoft.Tye.Hosting
{
    public sealed class MdnsApplicationAdvertiser : IApplicationAdvertiser
    {
        public void Advertise(WebApplication app)
        {
                var dashboardAddress = app.Addresses.First();
                var dashboardUri = new Uri(dashboardAddress, UriKind.Absolute);

                var discovery = new ServiceDiscovery();

                try {
                    var profile = new ServiceProfile($"tye-{dashboardUri.Port}", "_microsoft-tye._tcp", (ushort)dashboardUri.Port);

                    discovery.Advertise(profile);
                    discovery.Announce(profile);

                    app.ApplicationLifetime.ApplicationStopping.Register(
                        () =>
                        {
                            try
                            {
                                discovery.Unadvertise(profile);
                            }
                            finally
                            {
                                discovery.Dispose();
                            }
                        });
                }
                catch
                {
                    discovery.Dispose();

                    throw;
                }
        }
    }
}