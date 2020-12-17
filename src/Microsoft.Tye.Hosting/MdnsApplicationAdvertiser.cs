// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Makaretu.Dns;
using System;
using System.Threading.Tasks;

namespace Microsoft.Tye.Hosting
{
    public sealed class MdnsApplicationAdvertiser : IApplicationAdvertiser
    {
        public async Task AdvertiseWhileAsync(string name, Uri dashboard, Func<Task> task)
        {
                using (var discovery = new ServiceDiscovery())
                {
                    var profile = new ServiceProfile(name, "_microsoft-tye._tcp", (ushort)dashboard.Port);

                    discovery.Advertise(profile);
                    discovery.Announce(profile);

                    try
                    {
                        await task();
                    }
                    finally
                    {
                        discovery.Unadvertise(profile);
                    }
                }
        }
    }
}