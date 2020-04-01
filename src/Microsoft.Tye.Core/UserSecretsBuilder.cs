using System;
using System.IO;
using System.Text.Json;

namespace Microsoft.Tye
{
    internal class UserSecretsProvider : ISecretProvider
    {
        private readonly string secretId;

        public UserSecretsProvider(string secretId)
        {
            this.secretId = secretId;
        }

        public string GetSecretValue(string secretName)
        {
            var userSecretsPath = UserSecrets.GetUserSecretsPathFromSecrets();
            if (userSecretsPath is null)
            {
                throw new InvalidOperationException("User secrets path cannot be determined.");
            }
            var secretsPath = Path.Combine(userSecretsPath ?? string.Empty, secretId, "secrets.json");
            if (!File.Exists(secretsPath))
            {
                throw new FileNotFoundException($"User secrets file for secretId {secretId} does not exist.");
            }

            var jsonString = File.ReadAllText(secretsPath);
            using var document = JsonDocument.Parse(jsonString);
            var root = document.RootElement;
            var secretElement = root.GetProperty(secretName);
            return secretElement.GetString();
        }
    }
}
