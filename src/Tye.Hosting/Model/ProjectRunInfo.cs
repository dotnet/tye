namespace Micronetes.Hosting.Model
{
    public class ProjectRunInfo : RunInfo
    {
        public ProjectRunInfo(string project, string? args, bool build)
        {
            Project = project;
            Args = args;
            Build = build;
        }

        public string? Args { get; }
        public bool Build { get; }
        public string Project { get; }
    }
}
