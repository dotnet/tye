// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.Tye.ConfigModel
{
    public static class NameInferer
    {
        public static string? InferApplicationName(FileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                return null;
            }

            var extension = fileInfo.Extension;
            if (extension == ".sln" || extension == ".csproj" || extension == ".fsproj")
            {
                return Path.GetFileNameWithoutExtension(fileInfo.Name).ToLowerInvariant();
            }

            return fileInfo.Directory.Parent.Name.ToLowerInvariant();
        }
    }
}
