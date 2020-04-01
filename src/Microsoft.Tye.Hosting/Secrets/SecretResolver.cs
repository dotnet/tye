// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Tye.ConfigModel;
using Tye.Serialization;

namespace Microsoft.Tye.Hosting.Secrets
{

    public static class SecretResolver
    {
        public static string GetSecretValue(string providerName, string secretKey, FileInfo source)
        {
            var secretsFilePath = Path.Combine(source.Directory.FullName, "tye.secrets.yaml");

            if (!File.Exists(secretsFilePath))
            {
                throw new FileNotFoundException("Missing secrets config tye.secrets.yaml");
            }

            var provider = SecretProviderFactory.GetProvider(new FileInfo(secretsFilePath), providerName);

            return provider.GetSecretValue(secretKey);
        }
    }

    internal static class SecretProviderFactory
    {
        private static readonly Dictionary<string, ISecretProvider> providerCache = new Dictionary<string, ISecretProvider>();

        private static ConfigSecrets FromYaml(FileInfo file)
        {
            var parser = new YamlParser(file);
            var configSecrets = parser.ParseConfigSecrets();
            configSecrets.Source = file;
            return configSecrets;
        }

        internal static ISecretProvider GetProvider(FileInfo secretConfigSource, string secretProviderName)
        {
            if (providerCache.ContainsKey(secretProviderName))
            {
                return providerCache[secretProviderName];
            }

            var secretConfig = FromYaml(secretConfigSource);
            var providerConfig = secretConfig.Providers.SingleOrDefault(x => x.Name == secretProviderName);
            if (providerConfig is null)
            {
                throw new InvalidOperationException($"The secret provider {secretProviderName} is not configured in {secretConfigSource.FullName}.");
            }

            // This should be extensible so maybe 3rd party or community could register provider sources
            // but I'm hung up on all this statics right now
            ISecretProvider secretProvider;
            switch (providerConfig.Type)
            {
                case "user-secrets":
                    if (!providerConfig.Settings.ContainsKey("secretId"))
                    {
                        throw new InvalidOperationException($"Missing the secretId for user-secrets provider named {providerConfig.Name}");
                    }
                    var secretId = providerConfig.Settings["secretId"];
                    secretProvider = new UserSecretsProvider(secretId);
                    break;
                default:
                    throw new InvalidOperationException($"The secret provider type {providerConfig.Type} is not valid.");
            }

            providerCache.Add(secretProviderName, secretProvider);
            return secretProvider;
        }
    }

    public interface ISecretProvider
    {
        string GetSecretValue(string name);
    }
}
