// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Tye;
using Microsoft.Tye.ConfigModel;
using Microsoft.Tye.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace E2ETest
{
    public class TyeRunTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestOutputLogEventSink sink;

        public TyeRunTests(ITestOutputHelper output)
        {
            this.output = output;
            sink = new TestOutputLogEventSink(output);
        }

        [Fact]
        public async Task SingleProjectRunTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "single-project", "test-project"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "test-project.csproj"));
            using var host = new TyeHost(ConfigFactory.FromFile(projectFile).ToHostingApplication(), Array.Empty<string>())
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
                var dashboardResponse = await client.GetStringAsync(dashboardUri);

                // Only one service for single application.
                var service = host.Application.Services.First().Value;
                var binding = service.Description.Bindings.First();

                var protocol = binding.Protocol?.Length != 0 ? binding.Protocol : "http";
                var hostName = binding.Host != null && binding.Host.Length != 0 ? binding.Host : "localhost";

                var uriString = $"{protocol}://{hostName}:{binding.Port}";

                // Confirm that the uri is in the dashboard response.
                Assert.Contains(uriString, dashboardResponse);

                var uriBackendProcess = new Uri(uriString);

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
            using var host = new TyeHost(ConfigFactory.FromFile(projectFile).ToHostingApplication(), Array.Empty<string>())
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
                var dashboardResponse = await client.GetStringAsync(dashboardUri);

                await CheckServiceIsUp(host.Application, client, "backend", dashboardUri);
                await CheckServiceIsUp(host.Application, client, "frontend", dashboardUri);
            }
            finally
            {
                await host.StopAsync();
            }
        }

        private async Task CheckServiceIsUp(Microsoft.Tye.Hosting.Model.Application application, HttpClient client, string serviceName, Uri dashboardUri)
        {
            // make sure backend is up before frontend
            var service = application.Services.Where(a => a.Value.Description.Name == serviceName).First().Value;
            var binding = service.Description.Bindings.First();

            var protocol = binding.Protocol != null && binding.Protocol.Length != 0 ? binding.Protocol : "http";
            var hostName = binding.Host != null && binding.Host.Length != 0 ? binding.Host : "localhost";

            var uriString = $"{protocol}://{hostName}:{binding.Port}";

            // Confirm that the uri is in the dashboard response.

            var uriBackendProcess = new Uri(uriString);

            // This isn't reliable right now because micronetes only guarantees the process starts, not that
            // that kestrel started.
            try
            {
                var appResponse = await client.GetAsync(uriBackendProcess);
                Assert.Equal(HttpStatusCode.OK, appResponse.StatusCode);
                var content = await appResponse.Content.ReadAsStringAsync();
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
