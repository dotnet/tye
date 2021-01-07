namespace Tye
{
    public class ImageInfo
    {
        /// <summary>
        /// Gets or sets the name of the image. If null, the build image will be chosen
        /// based on the project configuration.
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Gets or sets the tag of the image. If null, the build image will be chosen
        /// based on the project configuration.
        /// </summary>
        public string? Tag { get; set; }
    }
}
