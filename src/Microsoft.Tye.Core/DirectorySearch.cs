// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Tye
{
    internal static class DirectorySearch
    {
        public static string? AscendingSearch(string directoryPath, string fileName)
        {
            if (directoryPath is null)
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            if (fileName is null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            var directory = new DirectoryInfo(directoryPath);

            while (true)
            {
                var filePath = Path.Combine(directory.FullName, fileName);
                if (File.Exists(filePath))
                {
                    return filePath;
                }

                if (directory == directory.Root)
                {
                    return null;
                }

                if (directory.Parent == null)
                {
                    return null;
                }

                directory = directory.Parent;
            }
        }

        public static IEnumerable<FileInfo> AscendingWildcardSearch(string directoryPath, string searchPattern)
        {
            if (directoryPath is null)
            {
                throw new ArgumentNullException(nameof(directoryPath));
            }

            if (searchPattern is null)
            {
                throw new ArgumentNullException(nameof(searchPattern));
            }

            var directory = new DirectoryInfo(directoryPath);

            while (true)
            {
                if (directory.EnumerateFiles(searchPattern).Any())
                {
                    return directory.EnumerateFiles(searchPattern);
                }

                if (directory == directory.Root)
                {
                    return Array.Empty<FileInfo>();
                }

                if (directory.Parent == null)
                {
                    return Array.Empty<FileInfo>();
                }

                directory = directory.Parent;
            }
        }
    }
}
