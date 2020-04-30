using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Tye;
using Microsoft.Tye.Hosting;
using Microsoft.Tye.Hosting.Model;
using Microsoft.Tye.Hosting.Model.V1;
using Test.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using static Test.Infrastructure.TestHelpers;

namespace E2ETest
{
    public class ReplicaStoppingTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TestOutputLogEventSink _sink;
        private readonly JsonSerializerOptions _options;

        public ReplicaStoppingTests(ITestOutputHelper output)
        {
            _output = output;
            _sink = new TestOutputLogEventSink(output);
            
            _options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            };

            _options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MultiProjectStoppingTests(bool docker)
        {
            using var projectDirectory = CopyTestProjectDirectory("multi-project");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler {ServerCertificateCustomValidationCallback = (a, b, c, d) => true, AllowAutoRedirect = false};

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, docker ? new[] {"--docker"} : Array.Empty<string>(), async (host, uri) =>
            {
                var replicaToStop = host.Application.Services["frontend"].Replicas.First();
                Assert.Equal(ReplicaState.Started, replicaToStop.Value.State);
                
                Assert.True(await DoOperationAndWaitForReplicasToRestart(host, new[] {replicaToStop.Key}, TimeSpan.FromSeconds(1), _ =>
                {
                    replicaToStop.Value.StoppingTokenSource.Cancel();
                    return Task.CompletedTask;
                }));

                Assert.Equal(ReplicaState.Removed, replicaToStop.Value.State);
                Assert.True(host.Application.Services.SelectMany(s => s.Value.Replicas).All(r => r.Value.State == ReplicaState.Started));
            });
        }

        private async Task<string> GetServiceUrl(HttpClient client, Uri uri, string serviceName)
        {
            var serviceResult = await client.GetStringAsync($"{uri}api/v1/services/{serviceName}");
            var service = JsonSerializer.Deserialize<V1Service>(serviceResult, _options);
            var binding = service.Description!.Bindings.Where(b => b.Protocol == "http").Single();
            return $"{binding.Protocol ?? "http"}://localhost:{binding.Port}";
        }
        
        private async Task RunHostingApplication(ApplicationBuilder application, string[] args, Func<TyeHost, Uri, Task> execute)
        {
            await using var host = new TyeHost(application.ToHostingApplication(), args)
            {
                Sink = _sink,
            };

            try
            {
                await StartHostAndWaitForReplicasToStart(host);

                var uri = new Uri(host.DashboardWebApplication!.Addresses.First());

                await execute(host, uri!);
            }
            finally
            {
                if (host.DashboardWebApplication != null)
                {
                    var uri = new Uri(host.DashboardWebApplication!.Addresses.First());

                    using var client = new HttpClient();

                    foreach (var s in host.Application.Services.Values)
                    {
                        var logs = await client.GetStringAsync(new Uri(uri, $"/api/v1/logs/{s.Description.Name}"));

                        _output.WriteLine($"Logs for service: {s.Description.Name}");
                        _output.WriteLine(logs);

                        var description = await client.GetStringAsync(new Uri(uri, $"/api/v1/services/{s.Description.Name}"));

                        _output.WriteLine($"Service defintion: {s.Description.Name}");
                        _output.WriteLine(description);
                    }
                }
            }
        }
    }
}
