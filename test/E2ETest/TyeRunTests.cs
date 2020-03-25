// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Tye;
using Microsoft.Tye.ConfigModel;
using Microsoft.Tye.Hosting;
using Microsoft.Tye.Hosting.Model;
using Microsoft.Tye.Hosting.Model.V1;
using Xunit;
using Xunit.Abstractions;

namespace E2ETest
{
    public class TyeRunTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestOutputLogEventSink sink;
        private readonly JsonSerializerOptions _options;

        public TyeRunTests(ITestOutputHelper output)
        {
            this.output = output;
            sink = new TestOutputLogEventSink(output);

            _options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            };

            _options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        [Fact]
        public async Task SingleProjectRunTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "single-project", "test-project"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "test-project.csproj"));
            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
            using var host = new TyeHost(application.ToHostingApplication(), Array.Empty<string>())
            {
                Sink = sink,
            };

            await host.StartAsync();
            try
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                    AllowAutoRedirect = false
                };

                var client = new HttpClient(new RetryHandler(handler));

                // Make sure dashboard and applications are up.
                // Dashboard should be hosted in same process.
                var dashboardUri = new Uri(host.DashboardWebApplication!.Addresses.First());
                var dashboardString = await client.GetStringAsync($"{dashboardUri}api/v1/services/test-project");

                var service = JsonSerializer.Deserialize<V1Service>(dashboardString, _options);
                var binding = service.Description!.Bindings.Where(b => b.Protocol == "http").Single();
                var uriBackendProcess = new Uri($"{binding.Protocol}://localhost:{binding.Port}");

                // This isn't reliable right now because micronetes only guarantees the process starts, not that
                // that kestrel started.
                try
                {
                    var appResponse = await client.GetAsync(uriBackendProcess);
                    Assert.Equal(HttpStatusCode.OK, appResponse.StatusCode);
                }
                finally
                {
                    // If we failed, there's a good chance the service isn't running. Let's get the logs either way and put
                    // them in the output.
                    var request = new HttpRequestMessage(HttpMethod.Get, new Uri(dashboardUri, $"/api/v1/logs/{service.Description.Name}"));
                    var response = await client.SendAsync(request);
                    var text = await response.Content.ReadAsStringAsync();

                    output.WriteLine($"Logs for service: {service.Description.Name}");
                    output.WriteLine(text);
                }
            }
            finally
            {
                await host.StopAsync();
            }
        }

        [Fact]
        public async Task FrontendBackendRunTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "frontend-backend"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
            using var host = new TyeHost(application.ToHostingApplication(), Array.Empty<string>())
            {
                Sink = sink,
            };

            await host.StartAsync();
            try
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                    AllowAutoRedirect = false
                };

                var client = new HttpClient(new RetryHandler(handler));

                var dashboardUri = new Uri(host.DashboardWebApplication!.Addresses.First());

                await CheckServiceIsUp(host.Application, client, "backend", dashboardUri);
                await CheckServiceIsUp(host.Application, client, "frontend", dashboardUri);
            }
            finally
            {
                await host.StopAsync();
            }
        }

        [Fact]
        public async Task IngressRunTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "apps-with-ingress"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
            using var host = new TyeHost(application.ToHostingApplication(), Array.Empty<string>())
            {
                Sink = sink,
            };

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            using var client = new HttpClient(new RetryHandler(handler));
            await host.StartAsync();
            var serviceApi = new Uri(host.DashboardWebApplication!.Addresses.First());

            try
            {
                var ingressService = await client.GetStringAsync($"{serviceApi}api/v1/services/ingress");

                var service = JsonSerializer.Deserialize<V1Service>(ingressService, _options);
                var binding = service.Description!.Bindings.Single();
                var ingressUri = $"http://localhost:{binding.Port}";

                var responseA = await client.GetAsync(ingressUri + "/A");
                var responseB = await client.GetAsync(ingressUri + "/B");

                Assert.StartsWith("Hello from Application A", await responseA.Content.ReadAsStringAsync());
                Assert.StartsWith("Hello from Application B", await responseB.Content.ReadAsStringAsync());

                var requestA = new HttpRequestMessage(HttpMethod.Get, ingressUri);
                requestA.Headers.Host = "a.example.com";
                var requestB = new HttpRequestMessage(HttpMethod.Get, ingressUri);
                requestB.Headers.Host = "b.example.com";

                responseA = await client.SendAsync(requestA);
                responseB = await client.SendAsync(requestB);

                Assert.StartsWith("Hello from Application A", await responseA.Content.ReadAsStringAsync());
                Assert.StartsWith("Hello from Application B", await responseB.Content.ReadAsStringAsync());
            }
            finally
            {
                // If we failed, there's a good chance the service isn't running. Let's get the logs either way and put
                // them in the output.
                foreach (var s in host.Application.Services.Values)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, new Uri(serviceApi, $"/api/v1/logs/{s.Description.Name}"));
                    var response = await client.SendAsync(request);
                    var text = await response.Content.ReadAsStringAsync();

                    output.WriteLine($"Logs for service: {s.Description.Name}");
                    output.WriteLine(text);
                }

                await host.StopAsync();
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        [SkipOnLinux]
        public async Task FrontendBackendRunTestWithDocker()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "frontend-backend"));
            using var tempDirectory = TempDirectory.Create(preferUserDirectoryOnMacOS: true);
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
            using var host = new TyeHost(application.ToHostingApplication(), new[] { "--docker" })
            {
                Sink = sink,
            };

            await host.StartAsync();
            try
            {
                // Make sure we're running containers
                Assert.True(host.Application.Services.All(s => s.Value.Description.RunInfo is DockerRunInfo));

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                    AllowAutoRedirect = false
                };

                var client = new HttpClient(new RetryHandler(handler));

                var dashboardUri = new Uri(host.DashboardWebApplication!.Addresses.First());

                await CheckServiceIsUp(host.Application, client, "backend", dashboardUri, timeout: TimeSpan.FromSeconds(60));
                await CheckServiceIsUp(host.Application, client, "frontend", dashboardUri, timeout: TimeSpan.FromSeconds(60));
            }
            finally
            {
                await host.StopAsync();
            }
        }

        [Fact]
        public async Task NullDebugTargetsDoesNotThrow()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "single-project", "test-project"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "test-project.csproj"));

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
            using var host = new TyeHost(application.ToHostingApplication(), Array.Empty<string>())
            {
                Sink = sink,
            };

            await host.StartAsync();

            await host.StopAsync();
        }

        private async Task CheckServiceIsUp(Microsoft.Tye.Hosting.Model.Application application, HttpClient client, string serviceName, Uri dashboardUri, TimeSpan? timeout = default)
        {
            // make sure backend is up before frontend
            var dashboardString = await client.GetStringAsync($"{dashboardUri}api/v1/services/{serviceName}");

            var service = JsonSerializer.Deserialize<V1Service>(dashboardString, _options);
            var binding = service.Description!.Bindings.Where(b => b.Protocol == "http").Single();
            var uriBackendProcess = new Uri($"{binding.Protocol}://localhost:{binding.Port}");

            var startTime = DateTime.UtcNow;
            try
            {
                // Wait up until the timeout to see if we can access the service.
                // For instance if we have to pull a base-image it can take a while.
                while (timeout.HasValue && startTime + timeout.Value > DateTime.UtcNow)
                {
                    try
                    {
                        await client.GetAsync(uriBackendProcess);
                        break;
                    }
                    catch (HttpRequestException)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3));
                    }
                }

                var appResponse = await client.GetAsync(uriBackendProcess);
                var content = await appResponse.Content.ReadAsStringAsync();
                output.WriteLine(content);
                Assert.Equal(HttpStatusCode.OK, appResponse.StatusCode);
                if (serviceName == "frontend")
                {
                    Assert.Matches("Frontend Listening IP: (.+)\n", content);
                    Assert.Matches("Backend Listening IP: (.+)\n", content);
                }
            }
            finally
            {
                // If we failed, there's a good chance the service isn't running. Let's get the logs either way and put
                // them in the output.
                foreach (var s in application.Services.Values)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, new Uri(dashboardUri, $"/api/v1/logs/{s.Description.Name}"));
                    var response = await client.SendAsync(request);
                    var text = await response.Content.ReadAsStringAsync();

                    output.WriteLine($"Logs for service: {s.Description.Name}");
                    output.WriteLine(text);
                }
            }
        }
    }
}
