﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Test.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using static Test.Infrastructure.TestHelpers;

namespace E2ETest
{

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
        public async Task FrontendBackendRunTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var frontendUri = await GetServiceUrl(client, uri, "frontend");
                var backendUri = await GetServiceUrl(client, uri, "backend");

                var backendResponse = await client.GetAsync(backendUri);
                var frontendResponse = await client.GetAsync(frontendUri);

                Assert.True(backendResponse.IsSuccessStatusCode);
                Assert.True(frontendResponse.IsSuccessStatusCode);
            });
        }

        [Fact(Skip = "Need to figure out how to install func before running")]
        public async Task FrontendBackendAzureFunctionTest()
        {
            // Install to directory
            using var tmp = TempDirectory.Create();
            await ProcessUtil.RunAsync("npm", "install azure-functions-core-tools@3`", workingDirectory: tmp.DirectoryPath);
            using var projectDirectory = CopyTestProjectDirectory("azure-functions");

            var content = @$"
# tye application configuration file
# read all about it at https://github.com/dotnet/tye
name: frontend-backend
services:
- name: backend
  azureFunction: backend/
  pathToFunc: {tmp.DirectoryPath}/node_modules/azure-functions-core-tools/bin/func.dll
- name: frontend
  project: frontend/frontend.csproj";

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            await File.WriteAllTextAsync(projectFile.FullName, content);
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var frontendUri = await GetServiceUrl(client, uri, "frontend");
                var backendUri = await GetServiceUrl(client, uri, "backend");

                var backendResponse = await client.GetAsync(backendUri);
                var frontendResponse = await client.GetAsync(frontendUri);

                Assert.True(backendResponse.IsSuccessStatusCode);
                Assert.True(frontendResponse.IsSuccessStatusCode);
            });
        }

        [ConditionalTheory]
        [SkipIfDockerNotRunning]
        [InlineData("single-project", "mcr.microsoft.com/dotnet/core/aspnet:3.1")]
        [InlineData("single-project-5.0", "mcr.microsoft.com/dotnet/aspnet:5.0")]
        public async Task SingleProjectWithDocker_UsesCorrectBaseImage(string projectName, string baseImage)
        {
            using var projectDirectory = CopyTestProjectDirectory(projectName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions() { Docker = true, }, async (app, uri) =>
            {
                // Make sure we're running containers
                Assert.True(app.Services.All(s => s.Value.Description.RunInfo is DockerRunInfo));

                // Ensure correct image used
                var dockerRunInfo = app.Services.Single().Value.Description.RunInfo as DockerRunInfo;
                Assert.Equal(baseImage, dockerRunInfo?.Image);

                // Ensure app runs
                var testProjectUri = await GetServiceUrl(client, uri, "test-project");
                var response = await client.GetAsync(testProjectUri);

                Assert.True(response.IsSuccessStatusCode);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task FrontendBackendRunTestWithDocker()
        {
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions() { Docker = true, }, async (app, uri) =>
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

        [ConditionalTheory]
        [SkipIfDockerNotRunning]
        [InlineData("Debug")]
        [InlineData("Release")]
        public async Task FrontendBackendRunTestWithDockerAndBuildConfigurationAsProperty(string buildConfiguration)
        {
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, $"tye-{buildConfiguration.ToLower()}-configuration.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions() { Docker = true, }, async (app, uri) =>
            {
                // Make sure we're running containers
                Assert.True(app.Services.All(s => s.Value.Description.RunInfo is DockerRunInfo));

                var frontendUri = await GetServiceUrl(client, uri, "frontend");
                var backendUri = await GetServiceUrl(client, uri, "backend");

                var backendResponse = await client.GetAsync(backendUri);
                var frontendResponse = await client.GetAsync(frontendUri);

                Assert.True(backendResponse.IsSuccessStatusCode);
                Assert.True(frontendResponse.IsSuccessStatusCode);

                Assert.True(app.Services.All(s => s.Value.Description.RunInfo != null && ((DockerRunInfo)s.Value.Description.RunInfo).VolumeMappings.Count > 0));

                var outputFileInfos = app.Services.Select(s => new FileInfo((s.Value?.Description?.RunInfo as DockerRunInfo)?.VolumeMappings[0].Source ?? throw new InvalidOperationException())).ToList();

                Assert.True(outputFileInfos.All(f => f.Directory?.Parent?.Parent?.Name == buildConfiguration));
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task FrontendProjectBackendDocker()
        {
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Transform the backend into a docker image for testing
            var project = (DotnetProjectServiceBuilder)application.Services.First(s => s.Name == "backend");
            application.Services.Remove(project);

            var outputFileName = project.AssemblyName + ".dll";
            var container = new ContainerServiceBuilder(project.Name, $"mcr.microsoft.com/dotnet/core/aspnet:{project.TargetFrameworkVersion}")
            {
                IsAspNet = true
            };
            container.Volumes.Add(new VolumeBuilder(project.PublishDir, name: null, target: "/app"));
            container.Args = $"dotnet /app/{outputFileName} {project.Args}";
            container.Bindings.AddRange(project.Bindings.Where(b => b.Protocol != "https"));

            await ProcessUtil.RunAsync("dotnet", $"publish \"{project.ProjectFile.FullName}\" /nologo", outputDataReceived: _sink.WriteLine, errorDataReceived: _sink.WriteLine);
            application.Services.Add(container);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
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
        public async Task FrontendDockerBackendProject()
        {
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Transform the backend into a docker image for testing
            var project = (DotnetProjectServiceBuilder)application.Services.First(s => s.Name == "frontend");
            application.Services.Remove(project);

            var outputFileName = project.AssemblyName + ".dll";
            var container = new ContainerServiceBuilder(project.Name, $"mcr.microsoft.com/dotnet/core/aspnet:{project.TargetFrameworkVersion}")
            {
                IsAspNet = true
            };
            container.Dependencies.UnionWith(project.Dependencies);
            container.Volumes.Add(new VolumeBuilder(project.PublishDir, name: null, target: "/app"));
            container.Args = $"dotnet /app/{outputFileName} {project.Args}";
            // We're not setting up the dev cert here
            container.Bindings.AddRange(project.Bindings.Where(b => b.Protocol != "https"));

            await ProcessUtil.RunAsync("dotnet", $"publish \"{project.ProjectFile.FullName}\" /nologo", outputDataReceived: _sink.WriteLine, errorDataReceived: _sink.WriteLine);
            application.Services.Add(container);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var frontendUri = await GetServiceUrl(client, uri, "frontend");
                var backendUri = await GetServiceUrl(client, uri, "backend");

                var backendResponse = await client.GetAsync(backendUri);
                var frontendResponse = await client.GetAsync(frontendUri);

                Assert.True(backendResponse.IsSuccessStatusCode);
                Assert.True(frontendResponse.IsSuccessStatusCode);
            });
        }

        [Fact]
        public async Task FrontendBackendWatchRunTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions() { Watch = true }, async (app, uri) =>
            {
                // make sure both are running
                var frontendUri = await GetServiceUrl(client, uri, "frontend");
                var backendUri = await GetServiceUrl(client, uri, "backend");

                var backendResponse = await client.GetAsync(backendUri);
                var frontendResponse = await client.GetAsync(frontendUri);

                Assert.True(backendResponse.IsSuccessStatusCode);
                Assert.True(frontendResponse.IsSuccessStatusCode);

                var startupPath = Path.Combine(projectDirectory.DirectoryPath, "frontend", "Startup.cs");
                File.AppendAllText(startupPath, "\n");

                const int retries = 10;
                for (var i = 0; i < retries; i++)
                {

                    var logs = await client.GetStringAsync(new Uri(uri, $"/api/v1/logs/frontend"));

                    // "Application Started" should be logged twice due to the file change
                    if (logs.IndexOf("Application started") != logs.LastIndexOf("Application started"))
                    {
                        return;
                    }

                    await Task.Delay(5000);
                }

                throw new Exception("Failed to relaunch project with dotnet watch");
            });
        }

        [Fact]
        public async Task WebAppWatchRunTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("web-app");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions() { Watch = true }, async (app, uri) =>
            {
                // make sure app is running
                var appUri = await GetServiceUrl(client, uri, "web-app");

                var response = await client.GetAsync(appUri);

                Assert.True(response.IsSuccessStatusCode);

                var startupPath = Path.Combine(projectDirectory.DirectoryPath, "Pages", "index.cshtml");
                File.AppendAllText(startupPath, "\n");

                const int retries = 10;
                for (var i = 0; i < retries; i++)
                {

                    var logs = await client.GetStringAsync(new Uri(uri, $"/api/v1/logs/web-app"));

                    // "Application Started" should be logged twice due to the file change
                    if (logs.IndexOf("Application started") != logs.LastIndexOf("Application started"))
                    {
                        return;
                    }

                    await Task.Delay(5000);
                }

                throw new Exception("Failed to relaunch project with dotnet watch");
            });
        }

        [Fact]
        public async Task DockerBaseImageAndTagTest()
        {
            using var projectDirectory = CopyTestProjectDirectory(Path.Combine("frontend-backend", "backend"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "backend-baseimage.csproj"));

            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Transform the backend into a docker image for testing
            var project = (DotnetProjectServiceBuilder)application.Services.First(s => s.Name == "backend-baseimage");

            // check ContainerInfo values
            Assert.True(string.Equals(project.ContainerInfo!.BaseImageName, "mcr.microsoft.com/dotnet/core/sdk"));
            Assert.True(string.Equals(project.ContainerInfo!.BaseImageTag, "3.1-buster"));

            // check projectInfo values
            var projectRunInfo = new ProjectRunInfo(project);

            Assert.True(string.Equals(projectRunInfo!.ContainerBaseImage, project.ContainerInfo.BaseImageName));
            Assert.True(string.Equals(projectRunInfo!.ContainerBaseTag, project.ContainerInfo.BaseImageTag));
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

            await RunHostingApplication(application, new HostOptions() { Docker = true, }, async (app, serviceApi) =>
            {
                var serviceUri = await GetServiceUrl(client, serviceApi, "volume-test");

                Assert.NotNull(serviceUri);

                var response = await client.GetAsync(serviceUri);

                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                await client.PostAsync(serviceUri, new StringContent("Things saved to the volume!"));

                Assert.Equal("Things saved to the volume!", await client.GetStringAsync(serviceUri));
            });

            await RunHostingApplication(application, new HostOptions() { Docker = true, }, async (app, serviceApi) =>
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
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

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

            try
            {
                await RunHostingApplication(
                    application,
                    new HostOptions() { Docker = true, },
                    async (app, uri) =>
                    {
                        // Make sure we're running containers
                        Assert.True(app.Services.All(s => s.Value.Description.RunInfo is DockerRunInfo));

                        foreach (var serviceBuilder in application.Services)
                        {
                            var serviceUri = await GetServiceUrl(client, uri, serviceBuilder.Name);
                            var serviceResponse = await client.GetAsync(serviceUri);
                            Assert.True(serviceResponse.IsSuccessStatusCode);

                            var serviceResult =
                                await client.GetStringAsync($"{uri}api/v1/services/{serviceBuilder.Name}");
                            var service = JsonSerializer.Deserialize<V1Service>(serviceResult, _options);

                            Assert.NotNull(service);

                            Assert.Equal(dockerNetwork, service!.Replicas!.FirstOrDefault().Value.DockerNetwork);
                        }
                    });
            }
            finally
            {
                // Delete the network
                await ProcessUtil.RunAsync("docker", $"network rm {dockerNetwork}");
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task DockerNetworkAssignmentForNonExistingNetworkTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var dockerNetwork = "tye_docker_network_test" + Guid.NewGuid().ToString().Substring(0, 10);
            application.Network = dockerNetwork;

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(
                application,
                new HostOptions() { Docker = true, },
                async (app, uri) =>
                {
                    // Make sure we're running containers
                    Assert.True(app.Services.All(s => s.Value.Description.RunInfo is DockerRunInfo));

                    foreach (var serviceBuilder in application.Services)
                    {
                        var serviceUri = await GetServiceUrl(client, uri, serviceBuilder.Name);
                        var serviceResponse = await client.GetAsync(serviceUri);
                        Assert.True(serviceResponse.IsSuccessStatusCode);

                        var serviceResult = await client.GetStringAsync($"{uri}api/v1/services/{serviceBuilder.Name}");
                        var service = JsonSerializer.Deserialize<V1Service>(serviceResult, _options);

                        Assert.NotNull(service);

                        Assert.NotEqual(dockerNetwork, service!.Replicas!.FirstOrDefault().Value.DockerNetwork);
                    }
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
            var project = (ProjectServiceBuilder)application.Services[0];

            using var tempDir = TempDirectory.Create(preferUserDirectoryOnMacOS: true);

            project.Volumes.Clear();
            project.Volumes.Add(new VolumeBuilder(source: tempDir.DirectoryPath, name: null, target: "/data"));

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            await File.WriteAllTextAsync(Path.Combine(tempDir.DirectoryPath, "file.txt"), "This content came from the host");

            var client = new HttpClient(new RetryHandler(handler));
            var options = new HostOptions()
            {
                Docker = true,
            };

            await RunHostingApplication(application, options, async (app, serviceApi) =>
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
            using var projectDirectory = CopyTestProjectDirectory("apps-with-ingress");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var ingressUri = await GetServiceUrl(client, uri, "ingress");
                var appAUri = await GetServiceUrl(client, uri, "appa");
                var appBUri = await GetServiceUrl(client, uri, "appb");

                var appAResponse = await client.GetAsync(appAUri);
                var appBResponse = await client.GetAsync(appBUri);

                Assert.True(appAResponse.IsSuccessStatusCode);
                Assert.True(appBResponse.IsSuccessStatusCode);

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

                // checking preservePath behavior
                var responsePreservePath = await client.GetAsync(ingressUri + "/C/test");
                Assert.Contains("Hit path /C/test", await responsePreservePath.Content.ReadAsStringAsync());
            });
        }

        [Fact]
        public async Task IngressQueryAndContentProxyingTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("apps-with-ingress");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var ingressUri = await GetServiceUrl(client, uri, "ingress");

                var request = new HttpRequestMessage(HttpMethod.Post, ingressUri + "/A/data?key1=value1&key2=value2");
                request.Content = new StringContent("some content");

                var response = await client.SendAsync(request);
                var responseContent =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());

                Assert.Equal("some content", responseContent!["content"]);
                Assert.Equal("?key1=value1&key2=value2", responseContent["query"]);
            });
        }

        [Fact]
        public async Task IngressStaticFilesTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("apps-with-ingress");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-ui.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = true
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var ingressUri = await GetServiceUrl(client, uri, "ingress");

                var htmlRequest = new HttpRequestMessage(HttpMethod.Get,
                    ingressUri + "/index.html");
                htmlRequest.Headers.Host = "ui.example.com";

                var htmlResponse = await client.SendAsync(htmlRequest);
                htmlResponse.EnsureSuccessStatusCode();
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task NginxIngressTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("nginx-ingress");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var nginxUri = await GetServiceUrl(client, uri, "nginx");
                var appAUri = await GetServiceUrl(client, uri, "appa");
                var appBUri = await GetServiceUrl(client, uri, "appb");

                var nginxResponse = await client.GetAsync(nginxUri);
                var appAResponse = await client.GetAsync(appAUri);
                var appBResponse = await client.GetAsync(appBUri);

                Assert.Equal(HttpStatusCode.NotFound, nginxResponse.StatusCode);
                Assert.True(appAResponse.IsSuccessStatusCode);
                Assert.True(appBResponse.IsSuccessStatusCode);

                var responseA = await client.GetAsync(nginxUri + "/A");
                var responseB = await client.GetAsync(nginxUri + "/B");

                Assert.StartsWith("Hello from Application A", await responseA.Content.ReadAsStringAsync());
                Assert.StartsWith("Hello from Application B", await responseB.Content.ReadAsStringAsync());
            });
        }

        [Fact]
        public async Task NullDebugTargetsDoesNotThrow()
        {
            using var projectDirectory = CopyTestProjectDirectory(Path.Combine("single-project", "test-project"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "test-project.csproj"));

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
            await using var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            await host.StartAsync();
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task MultiRepo_Works()
        {
            using var projectDirectory = CopyTestProjectDirectory(Path.Combine("multirepo"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "vote", "tye.yaml"));

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var votingUri = await GetServiceUrl(client, uri, "vote");
                var workerUri = await GetServiceUrl(client, uri, "worker");

                var votingResponse = await client.GetAsync(votingUri);
                var workerResponse = await client.GetAsync(workerUri);

                Assert.True(votingResponse.IsSuccessStatusCode);
                Assert.Equal(HttpStatusCode.NotFound, workerResponse.StatusCode);

                // results isn't running.
                var resultsResponse = await client.GetAsync($"{uri}api/v1/services/results");
                Assert.Equal(HttpStatusCode.NotFound, resultsResponse.StatusCode);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task MultiRepo_WorksWithCloning()
        {
            using var projectDirectory = TempDirectory.Create(preferUserDirectoryOnMacOS: true);

            var content = @"
name: VotingSample
services:
- name: vote
  repository: https://github.com/jkotalik/TyeMultiRepoVoting
- name: results
  repository: https://github.com/jkotalik/TyeMultiRepoResults";

            var yamlFile = Path.Combine(projectDirectory.DirectoryPath, "tye.yaml");
            var projectFile = new FileInfo(yamlFile);
            await File.WriteAllTextAsync(yamlFile, content);

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var votingUri = await GetServiceUrl(client, uri, "vote");
                var workerUri = await GetServiceUrl(client, uri, "worker");

                var votingResponse = await client.GetAsync(votingUri);
                var workerResponse = await client.GetAsync(workerUri);

                Assert.True(votingResponse.IsSuccessStatusCode);
                Assert.Equal(HttpStatusCode.NotFound, workerResponse.StatusCode);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task DockerFileTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("dockerfile");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
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
        public async Task DockerFileChangeContextTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("dockerfile");

            File.Move(Path.Combine(projectDirectory.DirectoryPath, "backend", "Dockerfile"), Path.Combine(projectDirectory.DirectoryPath, "Dockerfile"));

            var content = @"
name: frontend-backend
services:
- name: backend
  dockerFile: Dockerfile
  dockerFileContext: backend/
  bindings:
    - containerPort: 80
      protocol: http
- name: backend2
  dockerFile: ./Dockerfile
  dockerFileContext: ./backend
  bindings:
    - containerPort: 80
      protocol: http
- name: frontend
  project: frontend/frontend.csproj";

            var yamlFile = Path.Combine(projectDirectory.DirectoryPath, "tye.yaml");
            var projectFile = new FileInfo(yamlFile);
            await File.WriteAllTextAsync(yamlFile, content);

            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                var frontendUri = await GetServiceUrl(client, uri, "frontend");
                var backendUri = await GetServiceUrl(client, uri, "backend");
                var backend2Uri = await GetServiceUrl(client, uri, "backend2");

                var backendResponse = await client.GetAsync(backendUri);
                var backend2Response = await client.GetAsync(backend2Uri);
                var frontendResponse = await client.GetAsync(frontendUri);

                Assert.True(backendResponse.IsSuccessStatusCode);
                Assert.True(backend2Response.IsSuccessStatusCode);
                Assert.True(frontendResponse.IsSuccessStatusCode);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task RunExplicitYamlMultipleTargetFrameworksTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("multi-targetframeworks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-with-netcoreapp31.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                // make sure it is running
                var backendUri = await GetServiceUrl(client, uri, "multi-targetframeworks");

                var backendResponse = await client.GetAsync(backendUri);
                Assert.True(backendResponse.IsSuccessStatusCode);

                var responseContent = await backendResponse.Content.ReadAsStringAsync();
                Assert.Contains(".NET Core 3.1", responseContent);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task RunWithArgsMultipleTargetFrameworksTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("multi-targetframeworks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-no-buildproperties.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, "netcoreapp3.1");

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                // make sure it is running
                var backendUri = await GetServiceUrl(client, uri, "multi-targetframeworks");

                var backendResponse = await client.GetAsync(backendUri);
                Assert.True(backendResponse.IsSuccessStatusCode);

                var responseContent = await backendResponse.Content.ReadAsStringAsync();
                Assert.Contains(".NET Core 3.1", responseContent);
            });
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task RunCliArgDoesNotOverrideYamlMultipleTargetFrameworksTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("multi-targetframeworks");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-with-netcoreapp31.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, "netcoreapp2.1");

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true,
                AllowAutoRedirect = false
            };

            var client = new HttpClient(new RetryHandler(handler));

            await RunHostingApplication(application, new HostOptions(), async (app, uri) =>
            {
                // make sure it is running
                var backendUri = await GetServiceUrl(client, uri, "multi-targetframeworks");

                var backendResponse = await client.GetAsync(backendUri);
                Assert.True(backendResponse.IsSuccessStatusCode);

                var responseContent = await backendResponse.Content.ReadAsStringAsync();
                Assert.Contains(".NET Core 3.1", responseContent);
            });
        }

        private async Task<string> GetServiceUrl(HttpClient client, Uri uri, string serviceName)
        {
            var serviceResult = await client.GetStringAsync($"{uri}api/v1/services/{serviceName}");
            var service = JsonSerializer.Deserialize<V1Service>(serviceResult, _options);
            var binding = service!.Description!.Bindings!.Where(b => b.Protocol == "http").Single();
            return $"{binding.Protocol ?? "http"}://localhost:{binding.Port}";
        }

        private async Task RunHostingApplication(ApplicationBuilder application, HostOptions options, Func<Application, Uri, Task> execute)
        {
            await using var host = new TyeHost(application.ToHostingApplication(), options)
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
