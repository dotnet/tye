using System;

namespace Tye
{
    public sealed class DockerImage : Source
    {
        public DockerImage(string image)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            Image = image;
        }

        public string Image { get; }
    }
}
