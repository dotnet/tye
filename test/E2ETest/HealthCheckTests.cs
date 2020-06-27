// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-none.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-none" }, ReplicaState.Ready);
        }

        [Fact]
        public async Task ServiceWithoutLivenessShouldDefaultToHealthyTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-readiness.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-readiness" }, ReplicaState.Healthy);
        }

        [Fact]
        public async Task ServicWithoutLivenessShouldBecomeReadyWhenReadyTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-readiness.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-readiness" }, ReplicaState.Healthy);

            var replicas = host.Application.Services["health-readiness"].Replicas.Select(r => r.Value).ToList();
            Assert.True(await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Ready, replicas.Count, replicas.Select(r => r.Name).ToHashSet(), null, TimeSpan.Zero, async _ =>
            {
                await Task.WhenAll(replicas.Select(r => SetHealthyReadyInReplica(r, ready: true)));
            }));
        }

        [Fact]
        public async Task ServiceWithoutReadinessShouldDefaultToReadyWhenHealthyTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-liveness.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-liveness" }, ReplicaState.Started);

            var replicasToBecomeReady = host.Application.Services["health-liveness"].Replicas.Select(r => r.Value);
            var replicasNamesToBecomeReady = replicasToBecomeReady.Select(r => r.Name).ToHashSet();
            Assert.True(await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Ready, replicasNamesToBecomeReady.Count, replicasNamesToBecomeReady, null, TimeSpan.Zero, async _ =>
            {
                await Task.WhenAll(replicasToBecomeReady.Select(r => SetHealthyReadyInReplica(r, healthy: true)));
            }));
        }

        [Fact]
        public async Task ReadyServiceShouldBecomeHealthyWhenReadinessFailsTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-all.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            SetReplicasInitialState(host, true, true);

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-all" }, ReplicaState.Ready);

            var replicasToBecomeReady = host.Application.Services["health-all"].Replicas.Select(r => r.Value).ToList();
            var replicasNamesToBecomeReady = replicasToBecomeReady.Select(r => r.Name).ToHashSet();

            var randomReplica = replicasToBecomeReady[new Random().Next(0, replicasToBecomeReady.Count)];

            Assert.True(await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Healthy, 1, new[] { randomReplica.Name }.ToHashSet(), replicasNamesToBecomeReady.Where(r => r != randomReplica.Name).ToHashSet(), TimeSpan.FromSeconds(1), async _ =>
              {
                  await SetHealthyReadyInReplica(randomReplica, ready: false);
              }));
        }

        [Fact]
        public async Task ReadyServiceShouldRestartWhenLivenessFailsTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-all.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            SetReplicasInitialState(host, true, true);

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-all" }, ReplicaState.Ready);

            var replicasToBecomeReady = host.Application.Services["health-all"].Replicas.Select(r => r.Value).ToList();
            var replicasNamesToBecomeReady = replicasToBecomeReady.Select(r => r.Name).ToHashSet();

            var randomReplica = replicasToBecomeReady[new Random().Next(0, replicasToBecomeReady.Count)];

            Assert.True(await DoOperationAndWaitForReplicasToRestart(host, new[] { randomReplica.Name }.ToHashSet(), replicasNamesToBecomeReady.Where(r => r != randomReplica.Name).ToHashSet(), TimeSpan.FromSeconds(1), async _ =>
              {
                  await SetHealthyReadyInReplica(randomReplica, healthy: false);
              }));
        }

        [Fact]
        public async Task ProbeShouldRespectTimeoutTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-all.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            SetReplicasInitialState(host, true, true);

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-all" }, ReplicaState.Ready);

            var replicasToBecomeReady = host.Application.Services["health-all"].Replicas.Select(r => r.Value).ToList();
            var replicasNamesToBecomeReady = replicasToBecomeReady.Select(r => r.Name).ToHashSet();

            var randomNumber = new Random().Next(0, replicasToBecomeReady.Count);
            var randomReplica1 = replicasToBecomeReady[randomNumber];
            var randomReplica2 = replicasToBecomeReady[(randomNumber + 1) % replicasToBecomeReady.Count];

            Assert.True(await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Healthy, 1, new[] { randomReplica1.Name }.ToHashSet(), replicasNamesToBecomeReady.Where(r => r != randomReplica1.Name).ToHashSet(), TimeSpan.FromSeconds(2), async _ =>
               {
                   await Task.WhenAll(new[]
                   {
                    SetHealthyReadyInReplica(randomReplica1, readyDelay: 2),
                    SetHealthyReadyInReplica(randomReplica2, readyDelay: 1)
                   });
               }));
        }

        [Fact]
        public async Task ProxyShouldNotProxyToNonReadyReplicasTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-proxy.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            SetReplicasInitialState(host, true, true);

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-proxy" }, ReplicaState.Ready);

            var replicasToBecomeReady = host.Application.Services["health-proxy"].Replicas.Select(r => r.Value).ToList();

            // we assume that proxy will continue sending http request to the same replica
            var randomReplicaPortRes1 = await _client.GetAsync($"http://localhost:{host.Application.Services["health-proxy"].Description.Bindings.First().Port}/ports");
            var randomReplicaPort1 = JsonSerializer.Deserialize<int[]>(await randomReplicaPortRes1.Content.ReadAsStringAsync())![0];
            var randomReplica1 = replicasToBecomeReady.First(r => r.Bindings.Any(b => b.Port == randomReplicaPort1));

            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Healthy, 1, new[] { randomReplica1.Name }.ToHashSet(), null, TimeSpan.Zero, async _ =>
              {
                  await SetHealthyReadyInReplica(randomReplica1, ready: false);
              });

            var randomReplicaPortRes2 = await _client.GetAsync($"http://localhost:{host.Application.Services["health-proxy"].Description.Bindings.First().Port}/ports");
            var randomReplicaPort2 = JsonSerializer.Deserialize<int[]>(await randomReplicaPortRes2.Content.ReadAsStringAsync())![0];
            var randomReplica2 = replicasToBecomeReady.First(r => r.Bindings.Any(b => b.Port == randomReplicaPort2));

            Assert.NotEqual(randomReplicaPort1, randomReplicaPort2);

            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Healthy, 1, new[] { randomReplica2.Name }.ToHashSet(), null, TimeSpan.Zero, async _ =>
              {
                  await SetHealthyReadyInReplica(randomReplica2, ready: false);
              });

            try
            {
                var resShouldFail = await _client.GetAsync($"http://localhost:{host.Application.Services["health-proxy"].Description.Bindings.First().Port}/ports");
                Assert.False(resShouldFail.IsSuccessStatusCode);
            }
            catch (HttpRequestException)
            {
            }

            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Ready, 1, new[] { randomReplica2.Name }.ToHashSet(), null, TimeSpan.Zero, async _ =>
              {
                  await SetHealthyReadyInReplica(randomReplica2, ready: true);
              });

            var randomReplicaPortRes3 = await _client.GetAsync($"http://localhost:{host.Application.Services["health-proxy"].Description.Bindings.First().Port}/ports");
            var randomReplicaPort3 = JsonSerializer.Deserialize<int[]>(await randomReplicaPortRes3.Content.ReadAsStringAsync())![0];

            Assert.Equal(randomReplicaPort3, randomReplicaPort2);
        }

        [Fact]
        public async Task IngressShouldNotProxyToNonReadyReplicasTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-ingress.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            SetReplicasInitialState(host, true, true);

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-ingress-svc" }, ReplicaState.Ready);

            var replicasToBecomeReady = host.Application.Services["health-ingress-svc"].Replicas.Select(r => r.Value).ToList();
            var ingressBinding = host.Application.Services.First(s => s.Value.Description.RunInfo is IngressRunInfo).Value.Description.Bindings.First();
            var uniqueIdUrl = $"{ingressBinding.Protocol}://localhost:{ingressBinding.Port}/api/id";

            var uniqueIds = await ProbeNumberOfUniqueReplicas(uniqueIdUrl);
            Assert.Equal(2, uniqueIds);

            var firstReplica = replicasToBecomeReady.First();
            var secondReplica = replicasToBecomeReady.Skip(1).First();

            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Healthy, 1, new[] { firstReplica.Name }.ToHashSet(), null, TimeSpan.Zero, async _ =>
              {
                  await SetHealthyReadyInReplica(firstReplica, ready: false);
              });

            uniqueIds = await ProbeNumberOfUniqueReplicas(uniqueIdUrl);
            Assert.Equal(1, uniqueIds);

            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Healthy, 1, new[] { secondReplica.Name }.ToHashSet(), null, TimeSpan.Zero, async _ =>
              {
                  await SetHealthyReadyInReplica(secondReplica, ready: false);
              });

            var res = await _client.GetAsync(uniqueIdUrl);
            Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);

            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Ready, 2, new[] { firstReplica.Name, secondReplica.Name }.ToHashSet(), null, TimeSpan.Zero, async _ =>
              {
                  await SetHealthyReadyInReplica(firstReplica, ready: true);
                  await SetHealthyReadyInReplica(secondReplica, ready: true);
              });

            uniqueIds = await ProbeNumberOfUniqueReplicas(uniqueIdUrl);
            Assert.Equal(2, uniqueIds);
        }

        [Fact]
        public async Task HeadersTests()
        {
            using var projectDirectory = CopyTestProjectDirectory("health-checks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-all.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);

            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            SetReplicasInitialState(host, true, true);

            await StartHostAndWaitForReplicasToStart(host, new[] { "health-all" }, ReplicaState.Ready);

            var res = await _client.GetAsync($"http://localhost:{host.Application.Services["health-all"].Description.Bindings.First().Port}/livenessHeaders");
            Assert.True(res.IsSuccessStatusCode);

            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(await res.Content.ReadAsStringAsync());
            Assert.Equal("value1", headers!["name1"]);
            Assert.Equal("value2", headers["name2"]);

            res = await _client.GetAsync($"http://localhost:{host.Application.Services["health-all"].Description.Bindings.First().Port}/readinessHeaders");
            Assert.True(res.IsSuccessStatusCode);

            headers = JsonSerializer.Deserialize<Dictionary<string, string>>(await res.Content.ReadAsStringAsync());
            Assert.Equal("value3", headers!["name3"]);
            Assert.Equal("value4", headers["name4"]);
        }

        private async Task SetHealthyReadyInReplica(ReplicaStatus replica, bool? healthy = null, bool? ready = null, int? healthyDelay = null, int? readyDelay = null)
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

            if (healthyDelay.HasValue)
            {
                query.Add("healthyDelay=" + healthyDelay.Value);
            }

            if (readyDelay.HasValue)
            {
                query.Add("readyDelay=" + readyDelay.Value);
            }

            await _client.GetAsync($"http://localhost:{replica.Ports!.First()}/set?" + string.Join("&", query));
        }

        private async Task<int> ProbeNumberOfUniqueReplicas(string url)
        {
            // this assumes roundrobin
            var unique = new HashSet<string>();

            string? id = null;
            while (id == null || unique.Add(id))
            {
                var res = await _client.GetAsync(url);
                id = await res.Content.ReadAsStringAsync();
            }

            return unique.Count;
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
