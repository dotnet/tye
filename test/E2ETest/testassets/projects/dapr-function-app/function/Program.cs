using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal class Program
{
    private static void Main(string[] args)
    {
        var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(config =>
    {
        config.Services.AddLogging();
    })
    .Build();


        host.Run();
    }
}