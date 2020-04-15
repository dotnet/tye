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
<<<<<<< HEAD:src/Microsoft.Tye.Hosting/Secrets/SecretResolver.cs
            var parser = new YamlParser(file);
            var configSecrets = parser.ParseConfigSecrets();
            configSecrets.Source = file;
            return configSecrets;
=======
            var deserializer = new YamlDotNet.Serialization.Deserializer();

            using var reader = file.OpenText();
            try
            {
                var secretConfig = deserializer.Deserialize<SecretConfig>(reader);
                secretConfig.Source = file;

                //Deserialization makes all collection properties null so make sure they are non-null so
                //other code doesn't need to react
                foreach (var secretProvider in secretConfig.SecretProviders)
                {
                    secretProvider.Settings ??= new Dictionary<string, string>();
                }

                return secretConfig;
            }
            catch (Exception)
            {
                throw;
            }
>>>>>>> serializer namespace:src/Microsoft.Tye.Hosting/SecretResolver.cs
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

            // This should be more extensible so maybe 3rd party or community could register provider sources
            // but I'm hung up on all this statics right now.
            // Wonder if we can get these via nuget based on the provider type?
            ISecretProvider secretProvider;
            switch (providerConfig.Type)
            {
                case "user-secrets":
                    if (!providerConfig.Settings.ContainsKey("secretId"))
                    {
                        throw new InvalidOperationException($"Missing the secretId for user-secrets provider named {providerConfig.Name}");
                    }
                    secretProvider = new UserSecretsProvider(providerConfig.Settings["secretId"]);
                    break;
                case "key-vault":
                    if (!providerConfig.Settings.ContainsKey("vaultName"))
                    {
                        throw new InvalidOperationException($"Missing the vaultName for key-vault provider named {providerConfig.Name}");
                    }
                    secretProvider = new KeyVaultProvider(providerConfig.Settings["vaultName"]);
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
        string GetSecretValue(string secretName);
    }
}
