// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

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

    public class SecretEnvironmentVariableSource
    {
        public SecretEnvironmentVariableSource(string secretProvider, string secretKey, string envName, FileInfo appSource)
        {
            SecretProvider = secretProvider;
            SecretKey = secretKey;
            EnvName = envName;
            AppSource = appSource;
        }

        public string SecretProvider { get; }
        public string SecretKey { get; }
        public string EnvName { get; }
        public FileInfo AppSource { get; }
    }
}
