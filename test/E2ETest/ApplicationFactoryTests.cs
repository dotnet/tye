using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Tye;
using Test.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace E2ETest
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
        public async Task DoubleNestingWorks()
        {
            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(Path.Combine("multirepo"));
            var content = @"
name: VotingSample
services:
- name: vote
  include: vote/tye.yaml
- name: results
  include: results/tye.yaml";
            var yamlFile = Path.Combine(projectDirectory.DirectoryPath, "tye.yaml");
            await File.WriteAllTextAsync(yamlFile, content);

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, new FileInfo(yamlFile));

            Assert.Equal(5, application.Services.Count);

            var vote = application.Services.Single(s => s.Name == "vote");
            Assert.Equal(2, vote.Dependencies.Count);
            Assert.Contains("worker", vote.Dependencies);
            Assert.Contains("redis", vote.Dependencies);

            var results = application.Services.Single(s => s.Name == "results");
            Assert.Single(results.Dependencies);
            Assert.Contains("worker", results.Dependencies);

            var worker = application.Services.Single(s => s.Name == "worker");
            Assert.Equal(2, worker.Dependencies.Count);
            Assert.Contains("redis", worker.Dependencies);
            Assert.Contains("postgres", worker.Dependencies);

            var redis = application.Services.Single(s => s.Name == "redis");
            Assert.Equal(3, redis.Dependencies.Count);
            Assert.Contains("postgres", redis.Dependencies);
            Assert.Contains("worker", redis.Dependencies);
            Assert.Contains("vote", redis.Dependencies);

            var postgres = application.Services.Single(s => s.Name == "postgres");
            Assert.Equal(2, postgres.Dependencies.Count);
            Assert.Contains("worker", postgres.Dependencies);
            Assert.Contains("redis", postgres.Dependencies);
        }

        [Fact]
        public async Task SettingsFromFirstWins()
        {
            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(Path.Combine("multirepo"));
            var content = @"
name: VotingSample
services:
- name: include
  include: ../worker/tye.yaml
- name: results
  project: results.csproj
- name: redis
  image: redis2";
            var yamlFile = Path.Combine(projectDirectory.DirectoryPath, "results", "tye.yaml");
            await File.WriteAllTextAsync(yamlFile, content);

            // Debug targets can be null if not specified, so make sure calling host.Start does not throw.
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, new FileInfo(yamlFile));

            Assert.Equal(4, application.Services.Count);
            var redisService = application.Services.Single(s => s.Name == "redis");

            Assert.Equal("redis2", ((ContainerServiceBuilder)redisService).Image);
        }

        [Fact]
        public async Task WrongProjectPathProducesCorrectErrorMessage()
        {
            using var projectDirectory = TestHelpers.CopyTestProjectDirectory("frontend-backend");
            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-wrong-projectpath.yaml"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);

            var exception = await Assert.ThrowsAsync<CommandException>(async () =>
                await ApplicationFactory.CreateAsync(outputContext, projectFile));

            var wrongProjectPath = Path.Combine(projectDirectory.DirectoryPath, "backend1");
            Assert.Equal($"Failed to locate directory: '{wrongProjectPath}'.", exception.Message);
        }
    }
}
