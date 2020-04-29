using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Tye;
using Test.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace E2ETest
{
    public class ApplicationTests
    {
        private readonly TestOutputLogEventSink _sink;

        public ApplicationTests(ITestOutputHelper output)
        {
            _sink = new TestOutputLogEventSink(output);
        }

        [Fact]
        public async Task EnvironmentVariablesOnlySetForDirectDependencies()
        {
            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(Path.Combine("multirepo"));
            var yamlFile = Path.Combine(projectDirectory.DirectoryPath, "results", "tye.yaml");

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, new FileInfo(yamlFile));

            var app = application.ToHostingApplication();
            var dictionary = new Dictionary<string, string>();
            app.PopulateEnvironment(app.Services["results"], (s1, s2) => dictionary[s1] = s2);

            // Just the WORKER is defined.
            Assert.Equal(8, dictionary.Count);

            Assert.Equal("http", dictionary["SERVICE__WORKER__PROTOCOL"]);
            // No POSTGRES or REDIS
            Assert.False(dictionary.ContainsKey("SERVICE__POSTGRES__PROTOCOL"));
            Assert.False(dictionary.ContainsKey("SERVICE__REDIS__PROTOCOL"));
        }
    }
}
