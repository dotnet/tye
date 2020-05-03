using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Tye;
using Microsoft.Tye.Hosting;
using Microsoft.Tye.Hosting.Model;
using Test.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using static Test.Infrastructure.TestHelpers;

namespace E2ETest
{
    public class HealthCheckTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TestOutputLogEventSink _sink;
        private readonly JsonSerializerOptions _options;

        private static readonly ReplicaState?[] startedOrHigher = new ReplicaState?[] { ReplicaState.Started, ReplicaState.Healthy, ReplicaState.Ready };
        private static readonly ReplicaState?[] stoppedOrLower = new ReplicaState?[] { ReplicaState.Stopped, ReplicaState.Removed };

        private static HttpClient _client;

        static HealthCheckTests()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            _client = new HttpClient(new RetryHandler(handler));
        }

        public HealthCheckTests(ITestOutputHelper output)
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

        [Fact]
        public async Task ServiceWithoutLivenessReadinessShouldDefaultToReadyTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            var replicasToBeReady = host.Application.Services["none"].Replicas.Select(r => r.Value.Name).ToHashSet();
            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Ready, replicasToBeReady.Count, replicasToBeReady, null, TimeSpan.Zero, h => h.StartAsync());
        }

        [Fact]
        public async Task ServiceWithoutLivenessShouldDefaultToHealthyTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            var replicasToBeHealthy = host.Application.Services["readiness"].Replicas.Select(r => r.Value.Name).ToHashSet();
            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Healthy, replicasToBeHealthy.Count, replicasToBeHealthy, null, TimeSpan.Zero, h => h.StartAsync());
        }

        [Fact]
        public async Task ServiceWithoutReadinessShouldDefaultToReadyWhenHealthyTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            var replicasNamesToBeReady = host.Application.Services["liveness"].Replicas.Select(r => r.Value.Name).ToHashSet();
            var replicasToBeReady = host.Application.Services["liveness"].Replicas.Select(r => r.Value);
            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Started, replicasNamesToBeReady.Count, replicasNamesToBeReady, null, TimeSpan.Zero, h => h.StartAsync());

            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Ready, replicasNamesToBeReady.Count, replicasNamesToBeReady, null, TimeSpan.Zero, async _ =>
            {
                await Task.WhenAll(host.Application.Services["liveness"].Replicas.Select(r => r.Value).Select(r => SetHealthyReadyInReplica(r, healthy: true)));
            });
        }

        private async Task SetHealthyReadyInReplica(ReplicaStatus replica, bool? healthy = null, bool? ready = null)
        {
            var query = new List<string>();

            if (healthy.HasValue)
            {
                query.Add("healthy=" + healthy);
            }

            if (ready.HasValue)
            {
                query.Add("ready=" + ready);
            }

            await _client.GetAsync($"http://localhost:{replica.Ports.First()}/set?" + string.Join("&", query));
        }
    }
}
