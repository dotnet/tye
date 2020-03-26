// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class AggregateApplicationProcessor : IApplicationProcessor
    {
        private readonly IEnumerable<IApplicationProcessor> _applicationProcessors;
        public AggregateApplicationProcessor(IEnumerable<IApplicationProcessor> applicationProcessors)
        {
            _applicationProcessors = applicationProcessors;
        }

        public async Task StartAsync(Application application)
        {
            foreach (var processor in _applicationProcessors)
            {
                await processor.StartAsync(application);
            }
        }

        public async Task StopAsync(Application application)
        {
            // Shutdown in the opposite order
            foreach (var processor in _applicationProcessors.Reverse())
            {
                await processor.StopAsync(application);
            }
        }
    }
}
