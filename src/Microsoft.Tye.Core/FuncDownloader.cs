using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public static class FuncDownloader
    {
        // Same folder which VS installs azure apps to.
        private const string WindowsFuncDownloadLocation = "%LOCALAPPDATA%/AzureFunctionsTools/Tye";
        private const  string MacOSFuncDownloadLocation = "";
        private const  string LinuxFuncDownloadLocation = "";

        public const string DefaultFuncVersion = "v2";
        private const string V2Version = "2.7.2508";
        private const string V3Version = "3.0.2534";

        public static async Task<string> GetPathToFunc(string version = DefaultFuncVersion)
        {
            // Add logs
            // cancellation token
            var shortOSName = GetOsShortName();
            // function extension version
            // allow specifying path to func.exe
            string uri;
            string preciseVersion;
            // There doesn't seem to be a latest option. 
            switch (version)
            {
                case "v2":
                case "2":
                    preciseVersion = V2Version;
                    uri = $"https://functionscdn.azureedge.net/public/{V2Version}/Azure.Functions.Cli.{shortOSName}-x64.{V2Version}.zip";
                    break;
                case "v3":
                case "3":
                    preciseVersion = V3Version;
                    uri = $"https://functionscdn.azureedge.net/public/{V3Version}/Azure.Functions.Cli.{shortOSName}-x64.{V3Version}.zip";
                    break;
                default:
                    throw new NotSupportedException("Function version not supported.");
            }

            var directoryToInstallTo = GetDirectoryToInstallTo(preciseVersion);
            var funcPath = Path.Combine(directoryToInstallTo, "func.exe");

            // Check if exists first.
            if (Directory.Exists(directoryToInstallTo))
            {
                return funcPath;
            }

            var client = new HttpClient();
            var response = await client.GetAsync(uri);

            using (var tempFile = TempFile.Create())
            {
                {
                    var responseStream = await response.Content.ReadAsStreamAsync();
                    await using var stream = File.OpenWrite(tempFile.FilePath);
                    await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
                    await responseStream.CopyToAsync(stream);
                }

                // unzip to folder.
                ZipFile.ExtractToDirectory(tempFile.FilePath, directoryToInstallTo);
                return funcPath;
            }
        }

        private static string GetDirectoryToInstallTo(string preciseVersion)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Default is min.win for whatever reason, probably minified win
                return Environment.ExpandEnvironmentVariables(Path.Combine(WindowsFuncDownloadLocation, preciseVersion));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.ExpandEnvironmentVariables(Path.Combine(MacOSFuncDownloadLocation, preciseVersion));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Environment.ExpandEnvironmentVariables(Path.Combine(LinuxFuncDownloadLocation, preciseVersion));
            }
            else
            {
                throw new NotSupportedException("OS platform not supported.");
            }
        }

        private static string GetOsShortName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Default is min.win for whatever reason, probably minified win
                return "min.win";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "osx";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux";
            }
            else
            {
                throw new NotSupportedException("OS platform not supported.");
            }
        }
    }
}
