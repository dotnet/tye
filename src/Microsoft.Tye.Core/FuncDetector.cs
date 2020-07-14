// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Tye
{
    public class FuncDetector
    {
        // Same folder which VS installs azure apps to.
        private const string WindowsFuncDownloadLocation = "%LOCALAPPDATA%/AzureFunctionsTools/Tye";

        // REVIEW: Are these folders okay to download to?
        private const string MacOSFuncDownloadLocation = "~/.tye/AzureFunctionTools/Tye";
        private const string LinuxFuncDownloadLocation = "~/.tye/AzureFunctionTools/Tye";

        // Default to v3 as it's highly backwards compatible with v2. Diagnostics with v2
        // don't work by default as v2 uses 2.2. 
        private const string DefaultFuncVersion = "v3";

        private Dictionary<string, string> _pathsToFunc;

        internal FuncDetector()
        {
            _pathsToFunc = new Dictionary<string, string>();
        }

        public static FuncDetector Instance { get; } = new FuncDetector();

        public async Task<string> PathToFunc(string? version, string? arch, string? downloadPath, ILogger logger, CancellationToken cancellation, bool dryRun = false)
        {
            version = ValidateAndConvertVersion(version ?? DefaultFuncVersion, logger);

            // Default to x64
            arch = ValidateArch(arch ?? "x64");
            if (!_pathsToFunc.ContainsKey(version))
            {
                _pathsToFunc[version] = await GetPathToFunc(version, arch, downloadPath, logger, cancellation, dryRun);
            }

            return _pathsToFunc[version];
        }

        private string ValidateArch(string arch)
        {
            switch (arch)
            {
                case "x64":
                    return arch;
                case "x86":
                    return arch;
                default:
                    throw new NotSupportedException("Unrecognized architecture for function.");
            }
        }

        /// <summary>
        /// Function to convert versions to versions expected.
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        private string ValidateAndConvertVersion(string version, ILogger logger)
        {
            switch (version)
            {
                case "2":
                case "v2":
                    return "v2";
                case "3":
                case "v3":
                    return "v3";
                case "1":
                case "v1":
                    // TODO maybe don't throw here and just log a warning.
                    logger.LogWarning("Functions V1 are unsupported and untested in Tye. Use at your own risk!");
                    return "v1";
                default:
                    return version;
            }
        }

        private static async Task<string> GetPathToFunc(string version, string arch, string? downloadPath, ILogger logger, CancellationToken cancellation, bool dryRun)
        {
            var osName = GetOsName();
            using var client = new HttpClient();

            (var preciseVersion, var uri) = await GetDownloadInfo(version, client, arch, osName, logger, cancellation, dryRun);

            var directoryToInstallTo = downloadPath ?? GetAzureFunctionDirectoryWithVersion(preciseVersion);

            var funcPath = Path.Combine(directoryToInstallTo, GetFuncName());

            if (Directory.Exists(directoryToInstallTo) && File.Exists(funcPath))
            {
                return funcPath;
            }

            var response = await client.GetAsync(uri);

            if (dryRun)
            {
                return funcPath;
            }

            using (var tempFile = TempFile.Create())
            {
                {
                    var responseStream = await response.Content.ReadAsStreamAsync();
                    await using var stream = File.OpenWrite(tempFile.FilePath);
                    await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
                    await responseStream.CopyToAsync(stream);
                }

                ZipFile.ExtractToDirectory(tempFile.FilePath, directoryToInstallTo);
                return funcPath;
            }
        }

        private static async Task<(string, string)> GetDownloadInfo(string version, HttpClient client, string arch, string os, ILogger logger, CancellationToken cancellation, bool dryRun)
        {
            var directory = GetAzureFunctionDirectory();

            var feedJsonFile = Path.Combine(GetAzureFunctionDirectory(), "feed-v3.json");

            JToken json;
            if (File.Exists(feedJsonFile) && (DateTime.Now - File.GetLastWriteTimeUtc(feedJsonFile)).TotalDays < 90)
            {
                // don't bother rewriting it.
                using (JsonTextReader reader = new JsonTextReader(new StreamReader(feedJsonFile)))
                {
                    json = JObject.ReadFrom(reader);
                }
            }
            else
            {
                // Using VS/VSCode maintained list of function downloads
                var response = await client.GetAsync("https://go.microsoft.com/fwlink/?linkid=2109029", cancellation);

                var responseString = await response.Content.ReadAsStringAsync();
                using (JsonTextReader reader = new JsonTextReader(new StringReader(responseString)))
                {
                    json = JObject.ReadFrom(reader);
                }

                // Don't write file during dry run.
                if (!dryRun)
                {
                    Directory.CreateDirectory(new FileInfo(feedJsonFile).DirectoryName);
                    await File.WriteAllTextAsync(feedJsonFile, responseString, cancellation);
                }
            }

            // Get the version for the folder
            // and the download link for the zip.
            var versionInfo = (JValue)json["tags"][version]["release"];
            JValue? downloadLink;
            if (version == "v1")
            {
                downloadLink = (JValue)json["releases"][(string)versionInfo]["cli"];
            }
            else
            {
                downloadLink = (JValue)json["releases"][(string)versionInfo]["standaloneCli"]
                                            .Where(s => (((string)s["OS"])?.Equals(os) == true || ((string)s["OperatingSystem"])?.Equals(os) == true) && ((string)s["Architecture"]).Equals(arch))
                                            .Single()["downloadLink"];
            }

            return ((string)versionInfo, (string)downloadLink);
        }


        public static string GetAzureFunctionDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Default is min.win for whatever reason, probably minified win
                return Environment.ExpandEnvironmentVariables(WindowsFuncDownloadLocation);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Environment.ExpandEnvironmentVariables(MacOSFuncDownloadLocation);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Environment.ExpandEnvironmentVariables(LinuxFuncDownloadLocation);
            }
            else
            {
                throw new NotSupportedException("OS platform not supported.");
            }
        }

        public static string GetAzureFunctionDirectoryWithVersion(string preciseVersion)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Default is min.win for whatever reason, probably minified win
                return Environment.ExpandEnvironmentVariables(Path.Combine(WindowsFuncDownloadLocation, preciseVersion));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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

        public static string GetFuncName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "func.exe";
            }
            else
            {
                return "func";
            }
        }

        private static string GetOsName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Default is min.win for whatever reason, probably minified win
                return "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "MacOS";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }
            else
            {
                throw new NotSupportedException("OS platform not supported.");
            }
        }
    }
}
