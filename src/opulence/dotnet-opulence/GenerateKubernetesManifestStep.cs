using System.Threading.Tasks;

namespace Opulence
{
    public sealed class GenerateKubernetesManifestStep : ServiceExecutor.Step
    {
        public override string DisplayText => "Generating Manifests...";

        public string Environment { get; set; } = "production";


        public override Task ExecuteAsync(OutputContext output, Application application, ServiceEntry service)
        {
            if (SkipWithoutContainerOutput(output, service))
            {
                return Task.CompletedTask;
            }

            if (SkipForEnvironment(output, service, Environment))
            {
                return Task.CompletedTask;
            }

            service.Outputs.Add(KubernetesManifestGenerator.CreateDeployment(output, application, service));

            if (service.Service.Bindings.Count > 0)
            {
                service.Outputs.Add(KubernetesManifestGenerator.CreateService(output, application, service));
            }

            return Task.CompletedTask;
        }
    }
}
