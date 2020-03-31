// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.Tye
{
    internal class TempFile : IDisposable
    {
        public static TempFile Create()
        {
            return new TempFile(Path.GetTempFileName());
        }

        public void Dispose()
        {
            File.Delete(FilePath);
        }

        public TempFile(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

    }
}
