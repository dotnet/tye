using System.IO;

namespace Microsoft.Tye
{
    public class SecretEnvironmentVariableBuilder
    {
        public SecretEnvironmentVariableBuilder(string providerName, string providerKey, FileInfo appSource)
        {
            ProviderName = providerName;
            ProviderKey = providerKey;
            AppSource = appSource;
        }
        
        public string ProviderName { get; }
        public string ProviderKey { get; }
        public FileInfo AppSource { get; }
    }
}
