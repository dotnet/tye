// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Tye
{
    public class ConfigFileFinder
    {
        private static readonly string[] FileFormats = new[] { "tye.yaml", "tye.yml", "*.csproj", "*.fsproj", "*.sln" };

        public static bool TryFindSupportedFile(string directoryPath, out string? filePath, out string? errorMessage)
        {
            foreach (var format in FileFormats)
            {
                var files = Directory.GetFiles(directoryPath, format);

                if (files.Length == 1)
                {
                    errorMessage = null;
                    filePath = files[0];
                    return true;
                }

                if (files.Length > 1)
                {
                    errorMessage = $"More than one matching file was found in directory '{directoryPath}'.";
                    filePath = default;
                    return false;
                }
            }

            errorMessage = $"No project project file or solution was found in directory '{directoryPath}'.";
            filePath = default;
            return false;
        }
    }
}
