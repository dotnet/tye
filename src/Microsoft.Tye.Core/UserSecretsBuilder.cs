using System;
using System.IO;
using System.Text.Json;

namespace Microsoft.Tye
{
    internal static class UserSecretsBuilder
    {
        public static string GetSecretValue(string envSource)
        {
            var sep = '/';
            var index = envSource.IndexOf(sep);
            var secretId = envSource[1..index];
            var secretName = envSource[(index + 1)..];
            return GetSecretValue(secretId, secretName);
        }

        private static string GetSecretValue(string secretId, string secretName)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var jsonString = File.ReadAllText($@"{appData}\Microsoft\UserSecrets\{secretId}\secrets.json");
            // TODO: cache this document instead of parsing the file for every secret
            using var document = JsonDocument.Parse(jsonString);
            var root = document.RootElement;
            var secretElement = root.GetProperty(secretName);
            return secretElement.GetString();
        }
    }
}
