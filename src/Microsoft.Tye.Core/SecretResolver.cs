using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Microsoft.Tye.ConfigModel;
using Microsoft.Tye.Serialization;
using YamlDotNet.Serialization;

namespace Microsoft.Tye
{
    public class SecretConfig
    {
        [YamlIgnore]
        public FileInfo Source { get; set; } = default!;

        public string? Name { get; set; }

        public List<SecretProviderConfig> SecretProviders { get; set; } = new List<SecretProviderConfig>();
    }

    public class SecretProviderConfig
    {
        [Required]
        public string Name { get; set; } = default!;

        [Required]
        public string Type { get; set; } = default!;

        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        //public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }

    public static class SecretResolver
    {
        private static readonly SecretEnvironmentVariableBuilder[] EmptySecrets = Array.Empty<SecretEnvironmentVariableBuilder>();

        public static IEnumerable<SecretEnvironmentVariableBuilder> GetContainerSecrets(ContainerServiceBuilder container, FileInfo source)
        {
            var secretsFilePath = Path.Combine(source.Directory.FullName, "tye.secrets.yaml");

            if (!File.Exists(secretsFilePath))
            {
                return EmptySecrets;
            }

            var appConfig = ConfigFactory.FromFile(source);

            var containerConfig = appConfig.Services.SingleOrDefault(x => x.Name == container.Name);
            if (containerConfig is null)
            {
                return EmptySecrets;
            }


            var secrets = new List<SecretEnvironmentVariableBuilder>();
            foreach (var secretEnvironmentVariable in containerConfig.Configuration.Where(x => x.SecretProvider != null))
            {
                var provider = SecretProviderFactory.GetProvider(new FileInfo(secretsFilePath), secretEnvironmentVariable.SecretProvider);

                var value = provider.GetSecretValue(secretEnvironmentVariable.SecretKey);

                secrets.Add(new SecretEnvironmentVariableBuilder(secretEnvironmentVariable.Name, value));
            }

            return secrets;
        }
    }

    internal static class SecretProviderFactory
    {
        private static readonly Dictionary<string, ISecretProvider> providerCache = new Dictionary<string, ISecretProvider>();

        private static SecretConfig FromYaml(FileInfo file)
        {
            var deserializer = YamlSerializer.CreateDeserializer();

            using var reader = file.OpenText();
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

        internal static ISecretProvider GetProvider(FileInfo secretConfigSource, string secretProviderName)
        {
            if (providerCache.ContainsKey(secretProviderName))
            {
                return providerCache[secretProviderName];
            }

            var secretConfig = FromYaml(secretConfigSource);
            var providerConfig = secretConfig.SecretProviders.SingleOrDefault(x => x.Name == secretProviderName);
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

    public class SecretEnvironmentVariableBuilder
    {
        public SecretEnvironmentVariableBuilder(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; set; }
        public string Value { get; set; }
    }
}
