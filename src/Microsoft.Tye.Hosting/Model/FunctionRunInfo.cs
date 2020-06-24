// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye.Hosting.Model
{
    public class FunctionRunInfo : RunInfo
    {
        public FunctionRunInfo(FunctionServiceBuilder function)
        {
            Args = function.Args;
            FunctionPath = function.FunctionPath;
            Version = function.Version;
            Architecture = function.Architecture;
            FuncExecutablePath = function.FuncExecutablePath;
        }

        public string? Args { get; }
        public string FunctionPath { get; }
        public string? Version { get; }
        public string? Architecture { get; }
        public string? FuncExecutablePath { get; set; }
    }
}
