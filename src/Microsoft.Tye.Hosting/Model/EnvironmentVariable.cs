// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye.Hosting.Model
{
    public class EnvironmentVariable
    {
        public EnvironmentVariable(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public string? Value { get; set; }

        public EnvironmentVariableSource? Source { get; set; }
    }
}
