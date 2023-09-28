// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Tye
{
    public class AzureFunctionServiceBuilder : LaunchedServiceBuilder
    {
        public AzureFunctionServiceBuilder(string name, string path, ServiceSource source)
            : base(name, source)
        {
            FunctionPath = path;
        }

        public int Replicas { get; set; } = 1;
        public string? Args { get; set; }
        public string FunctionPath { get; }
        public string? FuncExecutablePath { get; set; }
        public string? ProjectFile { get; set; }
        public string? AzureFunctionsVersion { get; set; }
        public List<EnvironmentVariableBuilder> EnvironmentVariables { get; } = new List<EnvironmentVariableBuilder>();
    }
}
