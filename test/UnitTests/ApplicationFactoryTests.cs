using System.IO;
using System.Linq;
using System.Threading.Tasks;
using E2ETest;
using Microsoft.Tye;
using Microsoft.Tye.ConfigModel;
using Tye;
using Tye.Serialization;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static E2ETest.TestHelpers;

namespace UnitTests
{
    public class ApplicationFactoryTests
    {
        private readonly TestOutputLogEventSink _sink;

        public ApplicationFactoryTests(ITestOutputHelper output)
        {
            _sink = new TestOutputLogEventSink(output);
        }

        [Fact]
        public async Task MultiRepo_Cycles()
        {
            // tye.yaml
            //  tye.yaml loops to first tye.yaml

            using var projectDirectory = CopyTestProjectDirectory(Path.Combine("single-project", "test-project"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "test-project.csproj"));

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
        }

        [Fact]
        public async Task MultiRepo_RepeatedServices_FirstAppearanceWins()
        {
            // tye.yaml
            //  tye.yaml loops to first tye.yaml

            using var projectDirectory = CopyTestProjectDirectory(Path.Combine("single-project", "test-project"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "test-project.csproj"));

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
        }
    }
}
