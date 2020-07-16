// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class FuncFinder : IApplicationProcessor
    {
        public Task StartAsync(Application application)
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
                return Task.CompletedTask;
            }

            foreach (var func in functions)
            {
                func.FuncExecutablePath ??= FindFuncForVersion(func);
            }

            return Task.CompletedTask;
        }

        private string? FindFuncForVersion(AzureFunctionRunInfo func)
        {
            // Only looking for npm on linux.
            var npmFuncDllPath = GetFuncNpmPath();
            if (!File.Exists(npmFuncDllPath))
            {
                throw new FileNotFoundException("Could not find func installation. Please install the azure function core tools via: `npm install -g azure-functions-core-tools@3`");
            }
            return npmFuncDllPath;
        }

        private string GetFuncNpmPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Default is min.win for whatever reason, probably minified win
                return Environment.ExpandEnvironmentVariables("%APPDATA%/npm/node_modules/azure-functions-core-tools/bin/func.dll");
            }
            else
            {
                return Environment.ExpandEnvironmentVariables("/usr/local/lib/node_modules/azure-functions-core-tools/bin/func.dll");
            }
        }

        public Task StopAsync(Application application)
        {
            return Task.CompletedTask;
        }
    }
}
