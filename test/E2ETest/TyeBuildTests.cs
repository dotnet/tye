// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Tye;
using Xunit.Abstractions;

namespace E2ETest
{
    public partial class TyeBuildTests
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
            await DockerAssert.DeleteDockerImagesAsync(output, "test/test-project");

            var projectName = "single-project";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            application.Registry = new ContainerRegistry("test");

            try
            {
                await BuildHost.ExecuteBuildAsync(outputContext, application, environment, interactive: false);

                await DockerAssert.AssertImageExistsAsync(output, "test/test-project");
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, "test/test-project");
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task FrontendBackendBuildTest()
        {
            await DockerAssert.DeleteDockerImagesAsync(output, "test/backend");
            await DockerAssert.DeleteDockerImagesAsync(output, "test/frontend");

            var projectName = "frontend-backend";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            application.Registry = new ContainerRegistry("test");

            try
            {
                await BuildHost.ExecuteBuildAsync(outputContext, application, environment, interactive: false);

                await DockerAssert.AssertImageExistsAsync(output, "test/backend");
                await DockerAssert.AssertImageExistsAsync(output, "test/frontend");
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, "test/backend");
                await DockerAssert.DeleteDockerImagesAsync(output, "test/frontend");
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task MultipleProjectBuildTest()
        {
            await DockerAssert.DeleteDockerImagesAsync(output, "test/backend");
            await DockerAssert.DeleteDockerImagesAsync(output, "test/frontend");
            await DockerAssert.DeleteDockerImagesAsync(output, "test/worker");

            var projectName = "multi-project";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            application.Registry = new ContainerRegistry("test");

            try
            {
                await BuildHost.ExecuteBuildAsync(outputContext, application, environment, interactive: false);

                await DockerAssert.AssertImageExistsAsync(output, "test/backend");
                await DockerAssert.AssertImageExistsAsync(output, "test/frontend");
                await DockerAssert.AssertImageExistsAsync(output, "test/worker");
            }
            finally
            {
                await DockerAssert.AssertImageExistsAsync(output, "test/backend");
                await DockerAssert.AssertImageExistsAsync(output, "test/frontend");
                await DockerAssert.AssertImageExistsAsync(output, "test/worker");
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task BuildDoesNotRequireRegistry()
        {
            await DockerAssert.DeleteDockerImagesAsync(output, "test-project");

            var projectName = "single-project";
            var environment = "production";

            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", projectName));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            try
            {
                await BuildHost.ExecuteBuildAsync(outputContext, application, environment, interactive: false);

                await DockerAssert.AssertImageExistsAsync(output, "test-project");
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, "test-project");
            }
        }
    }
}
