// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Tye;
using Tye.ConfigModel;
using Tye.Hosting;
using Xunit;

namespace E2ETest
{
    public class TyeRunTest
    {
        [Fact]
        public async Task SingleProjectTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(GetSolutionRootDirectory("tye"), "samples", "single-project", "test-project"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "test-project.csproj"));

            var application = ConfigFactory.FromFile(projectFile);
            var host = new TyeHost(application.ToHostingApplication(), Array.Empty<string>());
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
                var dashboardResponse = await client.GetStringAsync(new Uri(host.DashboardWebApplication!.Addresses.First()));

                // Only one service for single application.
                var service = application.Services.First();
                var binding = service.Bindings.First();

                var protocol = binding.Protocol?.Length != 0 ? binding.Protocol : "http";
                var hostName = binding.Host != null && binding.Host.Length != 0 ? binding.Host : "localhost";

                var uriString = $"{protocol}://{hostName}:{binding.Port}";

                // Confirm that the uri is in the dashboard response.
                Assert.Contains(uriString, dashboardResponse);

                var uriBackendProcess = new Uri(uriString);

                // This isn't reliable right now because micronetes only guarantees the process starts, not that
                // that kestrel started.
                var appResponse = await client.GetAsync(uriBackendProcess);
                Assert.Equal(HttpStatusCode.OK, appResponse.StatusCode);
            }
            finally
            {
                await host.StopAsync();
            }
        }


        // https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/Testing/src/TestPathUtilities.cs
        // This can get into a bad pattern for having crazy paths in places. Eventually, especially if we use helix,
        // we may want to avoid relying on sln position.
        public static string GetSolutionRootDirectory(string solution)
        {
            var applicationBasePath = AppContext.BaseDirectory;
            var directoryInfo = new DirectoryInfo(applicationBasePath);

            do
            {
                var projectFileInfo = new FileInfo(Path.Combine(directoryInfo.FullName, $"{solution}.sln"));
                if (projectFileInfo.Exists)
                {
                    return projectFileInfo.DirectoryName;
                }

                directoryInfo = directoryInfo.Parent;
            }
            while (directoryInfo.Parent != null);

            throw new Exception($"Solution file {solution}.sln could not be found in {applicationBasePath} or its parent directories.");
        }
    }
}
