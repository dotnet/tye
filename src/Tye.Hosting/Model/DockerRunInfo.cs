namespace Micronetes.Hosting.Model
{
    public class DockerRunInfo : RunInfo
    {
        public DockerRunInfo(string image, string? args)
        {
            Image = image;
            Args = args;
        }

        public string? Args { get; }

        public string Image { get; }
    }
}
