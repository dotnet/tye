using System;
using System.IO;

namespace Tye
{
    internal class TempDirectory : IDisposable
    {
        public static TempDirectory Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(directoryPath);
            return new TempDirectory(directoryPath);
        }

        private TempDirectory(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
