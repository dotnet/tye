// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;
using Tye;
using Tye.ConfigModel;
using Xunit;
using Xunit.Abstractions;

namespace E2ETest
{
    public class TyeGenerateTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestOutputLogEventSink sink;

        public TyeGenerateTests(ITestOutputHelper output)
        {
            this.output = output;
            sink = new TestOutputLogEventSink(output);
        }

        [ConditionalFact]
        [SkipIfNoDocker]
        public async Task SingleProjectGenerateTest()
        {
            var projectName = "single-project";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var application = ConfigFactory.FromFile(projectFile);

            // Need to add docker registry for generate
            application.Registry = "test";

            await GenerateHost.ExecuteGenerateAsync(new OutputContext(sink, Verbosity.Debug), application, environment, interactive: false);

            // name of application is the folder
            var content = File.ReadAllText(Path.Combine(tempDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
            var expectedContent = File.ReadAllText($"testassets/generate/{projectName}.yaml");

            Assert.Equal(expectedContent, content);
        }

        [Fact]
        public async Task FrontendBackendGenerateTest()
        {
            var projectName = "frontend-backend";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var application = ConfigFactory.FromFile(projectFile);

            // Need to add docker registry for generate
            application.Registry = "test";

            await GenerateHost.ExecuteGenerateAsync(new OutputContext(sink, Verbosity.Debug), application, environment, interactive: false);

            // name of application is the folder
            var content = File.ReadAllText(Path.Combine(tempDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
            var expectedContent = File.ReadAllText($"testassets/generate/{projectName}.yaml");

            Assert.Equal(expectedContent, content);
        }

        [Fact]
        public async Task MultipleProjectGenerateTest()
        {
            var projectName = "multi-project";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var application = ConfigFactory.FromFile(projectFile);

            // Need to add docker registry for generate
            application.Registry = "test";

            await GenerateHost.ExecuteGenerateAsync(new OutputContext(sink, Verbosity.Debug), application, environment, interactive: false);

            // name of application is the folder
            var content = File.ReadAllText(Path.Combine(tempDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
            var expectedContent = File.ReadAllText($"testassets/generate/{projectName}.yaml");

            Assert.Equal(expectedContent, content);
        }
    }
}
