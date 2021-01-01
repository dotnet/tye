// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public interface IApplicationProcessor
    {
        Task StartAsync(Application application);

        Task StopAsync(Application application);

        Task StartAsync(Application application, Service service) => Task.CompletedTask;

        Task StopAsync(Service service) => Task.CompletedTask;
    }
}
