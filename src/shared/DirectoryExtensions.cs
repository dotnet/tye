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
        public static void DeleteDirectory(string d)
        {
            foreach (var sub in Directory.EnumerateDirectories(d))
            {
                DeleteDirectory(sub);
            }

            try
            {
                foreach (var f in Directory.EnumerateFiles(d))
                {
                    var fi = new FileInfo(f);
                    fi.Attributes = FileAttributes.Normal;
                    fi.Delete();
                }
                Directory.Delete(d);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to delete directory {d}: {e.Message}");
            }
        }
    }
}
