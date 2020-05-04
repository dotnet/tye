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

            await StartHostAndWaitForReplicasToStart(host, new[] { "none" }, ReplicaState.Ready);
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

            await StartHostAndWaitForReplicasToStart(host, new[] { "readiness" }, ReplicaState.Healthy);
        }

        [Fact]
        public async Task ServicWithoutLivenessShouldBecomeReadyWhenReadyTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            await StartHostAndWaitForReplicasToStart(host, new[] { "readiness" }, ReplicaState.Healthy);

            var replicas = host.Application.Services["readiness"].Replicas.Select(r => r.Value).ToList();
            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Ready, replicas.Count, replicas.Select(r => r.Name).ToHashSet(), null, TimeSpan.Zero, async _ =>
            {
                await Task.WhenAll(replicas.Select(r => SetHealthyReadyInReplica(r, ready: true)));
            });
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

            await StartHostAndWaitForReplicasToStart(host, new[] { "liveness" }, ReplicaState.Started);

            var replicasToBecomeReady = host.Application.Services["liveness"].Replicas.Select(r => r.Value);
            var replicasNamesToBecomeReady = replicasToBecomeReady.Select(r => r.Name).ToHashSet();
            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Ready, replicasNamesToBecomeReady.Count, replicasNamesToBecomeReady, null, TimeSpan.Zero, async _ =>
            {
                await Task.WhenAll(replicasToBecomeReady.Select(r => SetHealthyReadyInReplica(r, healthy: true)));
            });
        }

        [Fact]
        public async Task ReadyServiceShouldBecomeHealthyWhenReadinessFailsTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            SetReplicasInitialState(host, true, true);

            await StartHostAndWaitForReplicasToStart(host, new[] { "all" }, ReplicaState.Ready);

            var replicasToBecomeReady = host.Application.Services["all"].Replicas.Select(r => r.Value).ToList();
            var replicasNamesToBecomeReady = replicasToBecomeReady.Select(r => r.Name).ToHashSet();

            var randomReplica = replicasToBecomeReady[new Random().Next(0, replicasToBecomeReady.Count)];

            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Healthy, 1, new[] { randomReplica.Name }.ToHashSet(), replicasNamesToBecomeReady.Where(r => r != randomReplica.Name).ToHashSet(), TimeSpan.FromSeconds(1), async _ =>
              {
                  await SetHealthyReadyInReplica(randomReplica, ready: false);
              });
        }

        [Fact]
        public async Task ReadyServiceShouldRestartWhenLivenessFailsTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            SetReplicasInitialState(host, true, true);

            await StartHostAndWaitForReplicasToStart(host, new[] { "all" }, ReplicaState.Ready);

            var replicasToBecomeReady = host.Application.Services["all"].Replicas.Select(r => r.Value).ToList();
            var replicasNamesToBecomeReady = replicasToBecomeReady.Select(r => r.Name).ToHashSet();

            var randomReplica = replicasToBecomeReady[new Random().Next(0, replicasToBecomeReady.Count)];

            outputContext.WriteInfoLine("from test: random replica: (" + randomReplica.Name + ", " + randomReplica.State + ")");
            outputContext.WriteInfoLine("from test: all: " + string.Join(", ", replicasToBecomeReady.Select(r => "(" + r.Name + ", " + r.State + ")")));

            await DoOperationAndWaitForReplicasToRestart(host, new[] { randomReplica.Name }.ToHashSet(), replicasNamesToBecomeReady.Where(r => r != randomReplica.Name).ToHashSet(), TimeSpan.FromSeconds(1), async _ =>
              {
                  await SetHealthyReadyInReplica(randomReplica, healthy: false);
              });
        }

        [Fact]
        public async Task HeadersTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            SetReplicasInitialState(host, true, true);

            await StartHostAndWaitForReplicasToStart(host, new[] { "all" }, ReplicaState.Ready);

            var res = await _client.GetAsync($"http://localhost:{host.Application.Services["all"].Description.Bindings.First().Port}/livenessHeaders");
            Assert.True(res.IsSuccessStatusCode);

            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(await res.Content.ReadAsStringAsync());
            Assert.Equal("value1", headers["name1"]);
            Assert.Equal("value2", headers["name2"]);

            res = await _client.GetAsync($"http://localhost:{host.Application.Services["all"].Description.Bindings.First().Port}/readinessHeaders");
            Assert.True(res.IsSuccessStatusCode);

            headers = JsonSerializer.Deserialize<Dictionary<string, string>>(await res.Content.ReadAsStringAsync());
            Assert.Equal("value3", headers["name3"]);
            Assert.Equal("value4", headers["name4"]);
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

        private void SetReplicasInitialState(TyeHost host, bool? healthy, bool? ready, string[]? services = null)
        {
            if (services == null)
            {
                services = host.Application.Services.Select(s => s.Key).ToArray();
            }
            else
            {
                if (services.Any(s => !host.Application.Services.ContainsKey(s)))
                {
                    throw new ArgumentException($"not all services given in {nameof(services)} exist");
                }
            }

            foreach (var service in services)
            {
                if (healthy.HasValue)
                {
                    host.Application.Services[service].Description.Configuration.Add(new EnvironmentVariable("healthy") { Value = "true" });
                }

                if (ready.HasValue)
                {
                    host.Application.Services[service].Description.Configuration.Add(new EnvironmentVariable("ready") { Value = "true" });
                }
            }
        }
    }
}
