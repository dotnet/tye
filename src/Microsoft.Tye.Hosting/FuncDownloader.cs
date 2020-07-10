// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class FuncDownloader : IApplicationProcessor
    {
        private readonly ILogger _logger;

        public FuncDownloader(ILogger logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(Application application)
        {
            var functions = new HashSet<AzureFunctionRunInfo>();

            foreach (var s in application.Services)
            {
                if (s.Value.Description.RunInfo is AzureFunctionRunInfo function)
                {
                    functions.Add(function);
                }
            }

            // No functions
            if (functions.Count == 0)
            {
                return;
            }

            foreach (var func in functions)
            {
                func.FuncExecutablePath ??= await FuncDetector.Instance.PathToFunc(func.Version, func.Architecture, func.DownloadPath, _logger);
            }
        }

        public Task StopAsync(Application application)
        {
            return Task.CompletedTask;
        }
    }
}
