// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Tye.Hosting.Model;

namespace Tye.Hosting
{
    public interface IApplicationProcessor
    {
        Task StartAsync(Tye.Hosting.Model.Application application);

        Task StopAsync(Tye.Hosting.Model.Application application);
    }
}
