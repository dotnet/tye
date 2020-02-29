namespace Opulence
{
    public sealed class ApplicationGlobals
    {
        public DeploymentKind DeploymentKind { get; set; } = DeploymentKind.Kubernetes;

        public string? Name { get; set; }

        public ContainerRegistry? Registry { get; set; }
    }
}