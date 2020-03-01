using System;
using System.IO;

namespace Tye
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

        private TempFile(string filePath)
        {
            FilePath = filePath;
        }

        public string FilePath { get; }

    }
}
