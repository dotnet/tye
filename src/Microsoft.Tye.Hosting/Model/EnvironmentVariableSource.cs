// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye.Hosting.Model
{
    public class EnvironmentVariableSource
    {
        public EnvironmentVariableSource(string service, string? binding)
        {
            Service = service;
            Binding = binding;
        }

        public string Service { get; }
        
        public string? Binding { get; } 

        public SourceKind Kind { get; set; }

        public enum SourceKind
        {
            Url,
            Port,
            Host,
            ConnectionString,
        }
    }
}
