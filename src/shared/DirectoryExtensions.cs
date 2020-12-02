// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Tye
{
    internal static class DirectoryExtensions
    {
        // Calling Directory.Delete causes an exception for .git folders:
        //     System.UnauthorizedAccessException : Access to the path '17a475ecca365c678e907bd4c73e4c65b341c6' is denied.
        public static void DeleteDirectory(string directoryPath)
        {
            foreach (var subDirectoryPath in Directory.EnumerateDirectories(directoryPath))
            {
                DeleteDirectory(subDirectoryPath);
            }

            try
            {
                foreach (var filePath in Directory.EnumerateFiles(directoryPath))
                {
                    var fileInfo = new FileInfo(filePath)
                    {
                        Attributes = FileAttributes.Normal
                    };
                    fileInfo.Delete();
                }
                Directory.Delete(directoryPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($@"Failed to delete directory {directoryPath}: {e.Message}");
            }
        }
    }
}
