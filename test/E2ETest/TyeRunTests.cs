// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.IO;
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
using Microsoft.Tye.Hosting.Model.V1;
using Xunit;
using Xunit.Abstractions;
using static E2ETest.TestHelpers;

namespace E2ETest
{
    using Newtonsoft.Json;

    using JsonSerializer = System.Text.Json.JsonSerializer;

    public class TyeRunTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TestOutputLogEventSink _sink;
        private readonly JsonSerializerOptions _options;

        public TyeRunTests(ITestOutputHelper output)
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
        public async Task SingleProjectRunTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "single-project", "test-project"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "test-project.csproj"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, Array.Empty<string>(), async (app, uri) =>
            {
                var testUri = await GetServiceUrl(client, uri, "test-project");

                var testResponse = await client.GetAsync(testUri);

                Assert.True(testResponse.IsSuccessStatusCode);
            });
        }

        [Fact]
        public async Task FrontendBackendRunTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "frontend-backend"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, Array.Empty<string>(), async (app, uri) =>
            {
                var frontendUri = await GetServiceUrl(client, uri, "frontend");
                var backendUri = await GetServiceUrl(client, uri, "backend");

                var backendResponse = await client.GetAsync(backendUri);
                var frontendResponse = await client.GetAsync(frontendUri);

                Assert.True(backendResponse.IsSuccessStatusCode);
                Assert.True(frontendResponse.IsSuccessStatusCode);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task FrontendBackendRunTestWithDocker()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "frontend-backend"));
            using var tempDirectory = TempDirectory.Create(preferUserDirectoryOnMacOS: true);
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new[] { "--docker" }, async (app, uri) =>
            {
                // Make sure we're running containers
                Assert.True(app.Services.All(s => s.Value.Description.RunInfo is DockerRunInfo));

                var frontendUri = await GetServiceUrl(client, uri, "frontend");
                var backendUri = await GetServiceUrl(client, uri, "backend");

                var backendResponse = await client.GetAsync(backendUri);
                var frontendResponse = await client.GetAsync(frontendUri);

                Assert.True(backendResponse.IsSuccessStatusCode);
                Assert.True(frontendResponse.IsSuccessStatusCode);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task FrontendProjectBackendDocker()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "frontend-backend"));
            using var tempDirectory = TempDirectory.Create(preferUserDirectoryOnMacOS: true);
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Transform the backend into a docker image for testing
            var project = (ProjectServiceBuilder)application.Services.First(s => s.Name == "backend");
            application.Services.Remove(project);

            var outputFileName = project.AssemblyName + ".dll";
            var container = new ContainerServiceBuilder(project.Name, $"mcr.microsoft.com/dotnet/core/sdk:{project.TargetFrameworkVersion}");
            container.Volumes.Add(new VolumeBuilder(project.PublishDir, name: null, target: "/app"));
            container.Args = $"dotnet /app/{outputFileName} {project.Args}";
            container.Bindings.AddRange(project.Bindings);

            await ProcessUtil.RunAsync("dotnet", $"publish \"{project.ProjectFile.FullName}\" /nologo", outputDataReceived: _sink.WriteLine, errorDataReceived: _sink.WriteLine);
            application.Services.Add(container);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, Array.Empty<string>(), async (app, uri) =>
            {
                var frontendUri = await GetServiceUrl(client, uri, "frontend");
                var backendUri = await GetServiceUrl(client, uri, "backend");

                var backendResponse = await client.GetAsync(backendUri);
                var frontendResponse = await client.GetAsync(frontendUri);

                Assert.True(backendResponse.IsSuccessStatusCode);
                Assert.True(frontendResponse.IsSuccessStatusCode);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task DockerNamedVolumeTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("volume-test");
            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Add a volume
            var project = ((ProjectServiceBuilder)application.Services[0]);
            // Remove the existing volume so we can generate a random one for this test to avoid conflicts
            var volumeName = "tye_docker_volumes_test" + Guid.NewGuid().ToString().Substring(0, 10);
            project.Volumes.Clear();
            project.Volumes.Add(new VolumeBuilder(source: null, name: volumeName, "/data"));

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));
            var args = new[] { "--docker" };

            await RunHostingApplication(application, args, async (app, serviceApi) =>
            {
                var serviceUri = await GetServiceUrl(client, serviceApi, "volume-test");

                Assert.NotNull(serviceUri);

                var response = await client.GetAsync(serviceUri);

                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                await client.PostAsync(serviceUri, new StringContent("Things saved to the volume!"));

                Assert.Equal("Things saved to the volume!", await client.GetStringAsync(serviceUri));
            });

            await RunHostingApplication(application, args, async (app, serviceApi) =>
            {
                var serviceUri = await GetServiceUrl(client, serviceApi, "volume-test");

                Assert.NotNull(serviceUri);

                // The volume has data persisted
                Assert.Equal("Things saved to the volume!", await client.GetStringAsync(serviceUri));
            });

            // Delete the volume
            await ProcessUtil.RunAsync("docker", $"volume rm {volumeName}");
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task DockerNetworkAssignmentTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("network-test");
            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var dockerNetwork = "tye_docker_network_test" + Guid.NewGuid().ToString().Substring(0, 10);
            application.Network = dockerNetwork;

            // Create the existing network
            await ProcessUtil.RunAsync("docker", $"network create {dockerNetwork}");

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));
            var args = new[] { "--docker" };

            await RunHostingApplication(application, args, async (app, serviceApi) =>
            {
                var serviceResult = await client.GetStringAsync($"{serviceApi}api/v1/services/network-test");
                var service = JsonConvert.DeserializeObject<V1Service>(serviceResult);

                Assert.NotNull(service);

                Assert.Equal(dockerNetwork, service.Replicas.FirstOrDefault().Value.DockerNetwork);
            });


            // Delete the network
            await ProcessUtil.RunAsync("docker", $"network rm {dockerNetwork}");
        }


        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task DockerNetworkAssignmentForNonExistingNetworkTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("network-test");
            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var networkName = "tye_docker_network_test" + Guid.NewGuid().ToString().Substring(0, 10);
            application.Network = networkName;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));
            var args = new[] { "--docker" };

            await RunHostingApplication(application, args, async (app, serviceApi) =>
            {
                var serviceResult = await client.GetStringAsync($"{serviceApi}api/v1/services/network-test");
                var service = JsonConvert.DeserializeObject<V1Service>(serviceResult);

                Assert.NotNull(service);

                Assert.NotEqual(networkName, service.Replicas.FirstOrDefault().Value.DockerNetwork);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task DockerHostVolumeTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("volume-test");
            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Add a volume
            var project = ((ProjectServiceBuilder)application.Services[0]);

            using var tempDir = TempDirectory.Create();

            project.Volumes.Clear();
            project.Volumes.Add(new VolumeBuilder(source: tempDir.DirectoryPath, name: null, target: "/data"));

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            File.WriteAllText(Path.Combine(tempDir.DirectoryPath, "file.txt"), "This content came from the host");

            var client = new HttpClient(new RetryHandler(handler));
            var args = new[] { "--docker" };

            await RunHostingApplication(application, args, async (app, serviceApi) =>
            {
                var serviceUri = await GetServiceUrl(client, serviceApi, "volume-test");

                Assert.NotNull(serviceUri);

                // The volume has data the host mapped data
                Assert.Equal("This content came from the host", await client.GetStringAsync(serviceUri));
            });
        }

        [Fact]
        public async Task IngressRunTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "apps-with-ingress"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, Array.Empty<string>(), async (app, uri) =>
            {
                using var client = new HttpClient();

                var ingressUri = await GetServiceUrl(client, uri, "ingress");

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
            });
        }

        [Fact]
        public async Task NullDebugTargetsDoesNotThrow()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "single-project", "test-project"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "test-project.csproj"));

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
            await using var host = new TyeHost(application.ToHostingApplication(), Array.Empty<string>())
            {
                Sink = _sink,
            };

            await host.StartAsync();
        }

        private async Task<string> GetServiceUrl(HttpClient client, Uri uri, string serviceName)
        {
            var serviceResult = await client.GetStringAsync($"{uri}api/v1/services/{serviceName}");
            var service = JsonSerializer.Deserialize<V1Service>(serviceResult, _options);
            var binding = service.Description!.Bindings.Where(b => b.Protocol == "http").Single();
            return $"{binding.Protocol ?? "http"}://localhost:{binding.Port}";
        }

        private async Task RunHostingApplication(ApplicationBuilder application, string[] args, Func<Application, Uri, Task> execute)
        {
            await using var host = new TyeHost(application.ToHostingApplication(), args)
            {
                Sink = _sink,
            };

            try
            {
                await StartHostAndWaitForReplicasToStart(host);

                var uri = new Uri(host.DashboardWebApplication!.Addresses.First());

                await execute(host.Application, uri!);
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
