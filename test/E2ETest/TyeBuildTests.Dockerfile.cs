// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Tye;
using Test.Infrastructure;
using Xunit;
using static Test.Infrastructure.TestHelpers;

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

            application.Registry = new ContainerRegistry("test", null);

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

            application.Registry = new ContainerRegistry("test", null);

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
        public async Task TyeBuild_SinglePhase_ExistingDockerfileWithBuildArgs()
        {
            var projectName = "single-phase-dockerfile-args";
            var environment = "production";
            var imageName = "test/web";

            await DockerAssert.DeleteDockerImagesAsync(output, imageName);

            using var projectDirectory = CopyTestProjectDirectory(projectName);
            Assert.True(File.Exists(Path.Combine(projectDirectory.DirectoryPath, "Dockerfile")), "Dockerfile should exist.");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            application.Registry = new ContainerRegistry("test", null);

            try
            {
                await BuildHost.ExecuteBuildAsync(outputContext, application, environment, interactive: false);

                Assert.Single(application.Services.Single().Outputs.OfType<DockerImageOutput>());
                var builder = (DockerFileServiceBuilder)application.Services.First();
                var valuePair = builder.BuildArgs.First();
                Assert.Equal("pat", valuePair.Key);
                Assert.Equal("thisisapat", valuePair.Value);

                await DockerAssert.AssertImageExistsAsync(output, imageName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, imageName);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task TyeBuild_SinglePhase_ExistingDockerfileWithBuildArgsDuplicateArgs()
        {
            var projectName = "single-phase-dockerfile-args";
            var environment = "production";
            var imageName = "test/web";

            await DockerAssert.DeleteDockerImagesAsync(output, imageName);

            using var projectDirectory = CopyTestProjectDirectory(projectName);
            Assert.True(File.Exists(Path.Combine(projectDirectory.DirectoryPath, "Dockerfile")), "Dockerfile should exist.");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            application.Registry = new ContainerRegistry("test", null);

            try
            {
                await BuildHost.ExecuteBuildAsync(outputContext, application, environment, interactive: false);

                Assert.Single(application.Services.Single().Outputs.OfType<DockerImageOutput>());
                var builder = (DockerFileServiceBuilder)application.Services.First();
                var valuePair = builder.BuildArgs.First();
                Assert.Equal("pat", valuePair.Key);
                Assert.Equal("thisisapat", valuePair.Value);

                await DockerAssert.AssertImageExistsAsync(output, imageName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, imageName);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task TyeBuild_SinglePhase_ExistingDockerfileWithBuildArgsMultiArgs()
        {
            var projectName = "single-phase-dockerfile-multi-args";
            var environment = "production";
            var imageName = "test/web";

            await DockerAssert.DeleteDockerImagesAsync(output, imageName);

            using var projectDirectory = CopyTestProjectDirectory(projectName);
            Assert.True(File.Exists(Path.Combine(projectDirectory.DirectoryPath, "Dockerfile")), "Dockerfile should exist.");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            application.Registry = new ContainerRegistry("test", null);

            try
            {
                await BuildHost.ExecuteBuildAsync(outputContext, application, environment, interactive: false);

                Assert.Single(application.Services.Single().Outputs.OfType<DockerImageOutput>());
                var builder = (DockerFileServiceBuilder)application.Services.First();
                var valuePair = builder.BuildArgs.ElementAt(0);

                Assert.Equal(3, builder.BuildArgs.Count);
                Assert.Equal("pat", valuePair.Key);
                Assert.Equal("thisisapat", valuePair.Value);

                valuePair = builder.BuildArgs.ElementAt(1);
                Assert.Equal("number_of_replicas", valuePair.Key);
                Assert.Equal("2", valuePair.Value);

                valuePair = builder.BuildArgs.ElementAt(2);
                Assert.Equal("number_of_shards", valuePair.Key);
                Assert.Equal("5", valuePair.Value);

                await DockerAssert.AssertImageExistsAsync(output, imageName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, imageName);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task TyeBuild_MultipleTargetFrameworks_CliArgs()
        {
            var projectName = "multi-targetframeworks";
            var environment = "production";
            var imageName = "test/multi-targetframeworks";

            await DockerAssert.DeleteDockerImagesAsync(output, imageName);

            using var projectDirectory = CopyTestProjectDirectory(projectName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-no-buildproperties.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, "netcoreapp3.1");

            application.Registry = new ContainerRegistry("test", null);

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
        public async Task TyeBuild_MultipleTargetFrameworks_YamlBuildProperties()
        {
            var projectName = "multi-targetframeworks";
            var environment = "production";
            var imageName = "test/multi-targetframeworks";

            await DockerAssert.DeleteDockerImagesAsync(output, imageName);

            using var projectDirectory = CopyTestProjectDirectory(projectName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-with-netcoreapp31.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, "netcoreapp3.1");

            application.Registry = new ContainerRegistry("test", null);

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
