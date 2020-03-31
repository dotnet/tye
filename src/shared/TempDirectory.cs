// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Tye
{
    internal class TempDirectory : IDisposable
    {
        public static TempDirectory Create(bool preferUserDirectoryOnMacOS = false)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && preferUserDirectoryOnMacOS)
            {
                var baseDirectory = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                baseDirectory = Path.Combine(baseDirectory, ".tye");
                Directory.CreateDirectory(baseDirectory);

                var directoryPath = Path.Combine(baseDirectory, Path.GetRandomFileName());
                var directoryInfo = Directory.CreateDirectory(directoryPath);
                return new TempDirectory(directoryInfo);
            }
            else
            {
                var directoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                var directoryInfo = Directory.CreateDirectory(directoryPath);
                return new TempDirectory(directoryInfo);
            }
        }

        internal TempDirectory(DirectoryInfo directoryInfo)
        {
            DirectoryInfo = directoryInfo;

            DirectoryPath = directoryInfo.FullName;
        }

        public string DirectoryPath { get; }
        public DirectoryInfo DirectoryInfo { get; }

        public void Dispose()
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
