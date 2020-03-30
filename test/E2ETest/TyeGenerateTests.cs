// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Tye;
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
        [SkipIfDockerNotRunning]
        public async Task SingleProjectGenerateTest()
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

            // Need to add docker registry for generate
            application.Registry = new ContainerRegistry("test");

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(tempDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{projectName}.yaml");

                Assert.Equal(expectedContent.NormalizeNewLines(), content.NormalizeNewLines());
                await DockerAssert.AssertImageExistsAsync(output, "test/test-project");
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, "test/test-project");
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task FrontendBackendGenerateTest()
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

            // Need to add docker registry for generate
            application.Registry = new ContainerRegistry("test");

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(tempDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{projectName}.yaml");

                Assert.Equal(expectedContent.NormalizeNewLines(), content.NormalizeNewLines());

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
        public async Task MultipleProjectGenerateTest()
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

            // Need to add docker registry for generate
            application.Registry = new ContainerRegistry("test");

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(tempDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{projectName}.yaml");

                Assert.Equal(expectedContent.NormalizeNewLines(), content.NormalizeNewLines());

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
        public async Task GenerateWorksWithoutRegistry()
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
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(tempDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{projectName}-noregistry.yaml");

                Assert.Equal(expectedContent.NormalizeNewLines(), content.NormalizeNewLines());

                await DockerAssert.AssertImageExistsAsync(output, "test-project");
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, "test-project");
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task Generate_DaprApplication()
        {
            var applicationName = "dapr_test_application";
            var projectName = "dapr_test_project";
            var environment = "production";

            await DockerAssert.DeleteDockerImagesAsync(output, projectName);

            using var projectDirectory = TestHelpers.CopyTestProjectDirectory("dapr");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{applicationName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/dapr.yaml");

                Assert.Equal(expectedContent.NormalizeNewLines(), content.NormalizeNewLines());

                await DockerAssert.AssertImageExistsAsync(output, projectName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, projectName);
            }
        }
    }
}
