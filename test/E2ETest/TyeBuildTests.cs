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
    public class TyeBuildTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestOutputLogEventSink sink;

        public TyeBuildTests(ITestOutputHelper output)
        {
            this.output = output;
            sink = new TestOutputLogEventSink(output);
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task SingleProjectBuildTest()
        {
            var projectName = "single-project";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var application = ConfigFactory.FromFile(projectFile);

            application.Registry = "test";

            await BuildHost.ExecuteBuildAsync(new OutputContext(sink, Verbosity.Debug), application, environment, interactive: false);

            await DockerAssert.AssertImageExistsAsync(output, "test/test-project");
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task FrontendBackendBuildTest()
        {
            var projectName = "frontend-backend";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var application = ConfigFactory.FromFile(projectFile);

            application.Registry = "test";

            await BuildHost.ExecuteBuildAsync(new OutputContext(sink, Verbosity.Debug), application, environment, interactive: false);

            await DockerAssert.AssertImageExistsAsync(output, "test/backend");
            await DockerAssert.AssertImageExistsAsync(output, "test/frontend");
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task MultipleProjectBuildTest()
        {

            var projectName = "multi-project";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var application = ConfigFactory.FromFile(projectFile);

            application.Registry = "test";

            await BuildHost.ExecuteBuildAsync(new OutputContext(sink, Verbosity.Debug), application, environment, interactive: false);

            await DockerAssert.AssertImageExistsAsync(output, "test/backend");
            await DockerAssert.AssertImageExistsAsync(output, "test/frontend");
            await DockerAssert.AssertImageExistsAsync(output, "test/worker");
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task BuildDoesNotRequireRegistry()
        {
            var projectName = "single-project";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var application = ConfigFactory.FromFile(projectFile);

            await BuildHost.ExecuteBuildAsync(new OutputContext(sink, Verbosity.Debug), application, environment, interactive: false);

            await DockerAssert.AssertImageExistsAsync(output, "test-project");
        }
    }
}
