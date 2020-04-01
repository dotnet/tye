using System;
using System.IO;

namespace Microsoft.Tye
{
    public static class UserSecrets
    {
        public static string? GetUserSecretsPathFromSecrets()
        {
            // This is the logic used to determine the user secrets path
            // See https://github.com/dotnet/extensions/blob/64140f90157fec1bfd8aeafdffe8f30308ccdf41/src/Configuration/Config.UserSecrets/src/PathHelper.cs#L27
            const string userSecretsFallbackDir = "DOTNET_USER_SECRETS_FALLBACK_DIR";

            // For backwards compat, this checks env vars first before using Env.GetFolderPath
            var appData = Environment.GetEnvironmentVariable("APPDATA");
            var root = appData                                                                   // On Windows it goes to %APPDATA%\Microsoft\UserSecrets\
                       ?? Environment.GetEnvironmentVariable("HOME")                             // On Mac/Linux it goes to ~/.microsoft/usersecrets/
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                       ?? Environment.GetEnvironmentVariable(userSecretsFallbackDir);            // this fallback is an escape hatch if everything else fails

            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            return !string.IsNullOrEmpty(appData)
                ? Path.Combine(root, "Microsoft", "UserSecrets")
                : Path.Combine(root, ".microsoft", "usersecrets");
        }
    }
}
