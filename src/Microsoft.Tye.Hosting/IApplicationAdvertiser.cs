// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Tye.Hosting
{
    public interface IApplicationAdvertiser
    {
        Task AdvertiseWhileAsync(string name, Uri dashboard, Func<Task> task);
    }
}