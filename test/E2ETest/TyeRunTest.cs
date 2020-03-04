using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Tye.ConfigModel;
using Tye.Hosting;
using Tye.Hosting.Model;
using Xunit;

namespace E2ETest
{
    public class TyeRunTest
    {
        [Fact]
        public async Task SingleProjectTest()
        {
            var application = ConfigFactory.FromFile(new FileInfo(Path.Combine(GetSolutionRootDirectory("tye"), "samples", "single-project", "test-project", "test-project.csproj")));
            var host = new TyeHost(application.ToHostingApplication(), new string[0]);
            var webApplication = await host.StartAsync();
            try
            {
                var client = new HttpClient(new RetryHandler(new SocketsHttpHandler()));

                // Make sure dashboard and applications are up.
                // Dashboard should be hosted in same process.
                var dashboardResponse = await client.GetStringAsync(new Uri(webApplication.Addresses.First()));

                // Only one service for single application.
                var service = application.Services.First();
                var binding = service.Bindings.First();

                var protocol = binding.Protocol?.Length != 0 ? binding.Protocol : "http";
                var hostName = binding.Host != null && binding.Host.Length != 0 ? binding.Host : "localhost";

                var uriString = $"{protocol}://{hostName}:{binding.Port}";

                // Confirm that the uri is in the dashboard response.
                Assert.Contains(uriString, dashboardResponse);

                var uriBackendProcess = new Uri(uriString);

                // send request to service.
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
