// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Tye
{
    internal class TempDirectory : IDisposable
    {
        public static TempDirectory Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var directoryInfo = Directory.CreateDirectory(directoryPath);
            return new TempDirectory(directoryPath, directoryInfo);
        }

        private TempDirectory(string directoryPath, DirectoryInfo directoryInfo)
        {
            DirectoryPath = directoryPath;
            DirectoryInfo = directoryInfo;
        }

        public string DirectoryPath { get; }
        public DirectoryInfo DirectoryInfo { get; }

        public void Dispose()
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
