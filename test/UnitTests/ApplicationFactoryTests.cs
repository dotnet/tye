using System.IO;
using System.Threading.Tasks;
using Microsoft.Tye;
using Test.Infrastucture;
using Xunit;
using Xunit.Abstractions;

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
        public async Task CyclesStopImmediately()
        {
            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(Path.Combine("single-project"));
            var content = @"
name: single-project
services:
- name: test-project
  include: tye.yaml";
            var yamlFile = Path.Combine(projectDirectory.DirectoryPath, "tye.yaml");

            await File.WriteAllTextAsync(yamlFile, content);
            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, new FileInfo(yamlFile));

            Assert.Empty(application.Services);
        }

        [Fact]
        public async Task MultiRepo_RepeatedServices_FirstAppearanceWins()
        {
            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(Path.Combine("single-project", "test-project"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "test-project.csproj"));

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
        }
    }
}
