namespace Tye
{
    public class ImageInfo
    {
        /// <summary>
        /// Gets or sets the name of the base image. If null, the base image will be chosen
        /// based on the project configuration.
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the build image. If null, the build image will be chosen
        /// based on the project configuration.
        /// </summary>
        public string? Tag { get; set; }
    }
}
