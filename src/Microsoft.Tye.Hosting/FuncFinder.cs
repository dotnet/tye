// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Tye.Hosting.Model;

namespace Microsoft.Tye.Hosting
{
    public class FuncFinder : IApplicationProcessor
    {
        private ILogger _logger;

        public FuncFinder(ILogger logger)
        {
            _logger = logger;
        }

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
            int version = 4; // Default to the latest version (v4).
            
            if (!string.IsNullOrWhiteSpace(func.AzureFunctionsVersion))
            {
                var match = Regex.Match(func.AzureFunctionsVersion, @"^[vV](?<number>\d+)$");

                if (match.Success && int.TryParse(match.Groups["number"].Value, out int parsedValue))
                {
                    version = parsedValue;
                }
            }

            var funcDllPath = FuncDllPath(version);
            if (!File.Exists(funcDllPath))
            {
                throw new FileNotFoundException("Could not find func installation. Please install the azure function core tools with the installer: " +
                    $"https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local or `npm install -g azure-functions-core-tools@{version}`");
            }

            _logger.LogDebug("Using func for running azure functions located at {Func}.", funcDllPath);
            return funcDllPath;
        }

        private string FuncDllPath(int version)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var funcPathStandalone = Environment.ExpandEnvironmentVariables("%PROGRAMFILES%/Microsoft/Azure Functions Core Tools/func.exe");
                if (File.Exists(funcPathStandalone))
                {
                    return funcPathStandalone;
                }

                return Environment.ExpandEnvironmentVariables("%APPDATA%/npm/node_modules/azure-functions-core-tools/bin/func.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var funcPathStandalone = $"/usr/lib/azure-functions-core-tools-{version}/func";
                if (File.Exists(funcPathStandalone))
                {
                    return funcPathStandalone;
                }

                return "/usr/local/lib/node_modules/azure-functions-core-tools/bin/func";
            }
            else
            {
                // For brew path, just find a folder that supports functions v{version}.
                var funcDirectories = Directory.GetDirectories($"/usr/local/Cellar/azure-functions-core-tools@{version}/");
                var funcPathStandalone = Path.Combine(funcDirectories.LastOrDefault() ?? "", "func");
                if (File.Exists(funcPathStandalone))
                {
                    return funcPathStandalone;
                }

                return "/usr/local/lib/node_modules/azure-functions-core-tools/bin/func";
            }
        }

        public Task StopAsync(Application application)
        {
            return Task.CompletedTask;
        }
    }
}
