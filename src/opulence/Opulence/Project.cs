using System;

namespace Opulence
{
    public sealed class Project : Source
    {
        public Project(string relativeFilePath)
        {
            if (relativeFilePath is null)
            {
                throw new ArgumentNullException(nameof(relativeFilePath));
            }

            RelativeFilePath = relativeFilePath;
        }

        public string RelativeFilePath { get; }

        public FrameworkCollection Frameworks { get; } = new FrameworkCollection();

        public string TargetFramework { get; set; } = default!;

        public string Version { get; set; } = default!;
    }
}