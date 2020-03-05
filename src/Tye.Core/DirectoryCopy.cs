﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Tye
{
    public static class DirectoryCopy
    {
        public static void Copy(string source, string destination)
        {
            CopyCore(new DirectoryInfo(source), new DirectoryInfo(destination));
        }

        private static void CopyCore(DirectoryInfo source, DirectoryInfo destination)
        {
            Directory.CreateDirectory(destination.FullName);

            foreach (var file in source.GetFiles())
            {
                file.CopyTo(Path.Combine(destination.FullName, file.Name), true);
            }

            foreach (var nextSource in source.GetDirectories())
            {
                var nextDestination = destination.CreateSubdirectory(nextSource.Name);
                CopyCore(nextSource, nextDestination);
            }
        }
    }
}
