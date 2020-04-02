using System;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Microsoft.Tye.Hosting.Secrets
{
    public class KeyVaultProvider : ISecretProvider
    {
        private readonly string vaultName;

        public KeyVaultProvider(string vaultName)
        {
            this.vaultName = vaultName;
        }

        public string GetSecretValue(string secretName)
        {
            var kvUri = $"https://{vaultName}.vault.azure.net";
            var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());
            KeyVaultSecret secret = client.GetSecret(secretName);
            return secret.Value;
        }
    }
}
