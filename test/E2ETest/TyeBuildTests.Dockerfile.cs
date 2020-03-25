// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Tye;
using Xunit;
using static E2ETest.TestHelpers;

namespace E2ETest
{
    // Tests permutations of our Dockerfile-related behaviors.
    public partial class TyeBuildTests
    {
        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task TyeBuild_SinglePhase_GeneratedDockerfile()
        {
            var projectName = "single-phase-dockerfile";
            var environment = "production";
            var imageName = "test/single-phase-dockerfile";

            await DockerAssert.DeleteDockerImagesAsync(output, imageName);

            using var projectDirectory = CopyTestProjectDirectory(projectName);
            File.Delete(Path.Combine(projectDirectory.DirectoryPath, "Dockerfile"));
            Assert.False(File.Exists(Path.Combine(projectDirectory.DirectoryPath, "Dockerfile")), "Dockerfile should be gone.");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            application.Registry = new ContainerRegistry("test");

            try
            {
                await BuildHost.ExecuteBuildAsync(outputContext, application, environment, interactive: false);

                var publishOutput = Assert.Single(application.Services.Single().Outputs.OfType<ProjectPublishOutput>());
                Assert.False(Directory.Exists(publishOutput.Directory.FullName), $"Directory {publishOutput.Directory.FullName} should be deleted.");

                await DockerAssert.AssertImageExistsAsync(output, imageName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, imageName);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task TyeBuild_SinglePhase_ExistingDockerfile()
        {
            var projectName = "single-phase-dockerfile";
            var environment = "production";
            var imageName = "test/single-phase-dockerfile";

            await DockerAssert.DeleteDockerImagesAsync(output, imageName);

            using var projectDirectory = CopyTestProjectDirectory(projectName);
            Assert.True(File.Exists(Path.Combine(projectDirectory.DirectoryPath, "Dockerfile")), "Dockerfile should exist.");


            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            application.Registry = new ContainerRegistry("test");

            try
            {
                await BuildHost.ExecuteBuildAsync(outputContext, application, environment, interactive: false);

                var publishOutput = Assert.Single(application.Services.Single().Outputs.OfType<ProjectPublishOutput>());
                Assert.False(Directory.Exists(publishOutput.Directory.FullName), $"Directory {publishOutput.Directory.FullName} should be deleted.");

                await DockerAssert.AssertImageExistsAsync(output, imageName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, imageName);
            }
        }
    }
}
