// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Tye;
using Test.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using static Test.Infrastructure.TestHelpers;

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

            using var projectDirectory = CopyTestProjectDirectory(projectName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Need to add docker registry for generate
            application.Registry = new ContainerRegistry("test", null);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{projectName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);

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

            using var projectDirectory = CopyTestProjectDirectory(projectName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Need to add docker registry for generate
            application.Registry = new ContainerRegistry("test", null);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{projectName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);

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

            using var projectDirectory = CopyTestProjectDirectory(projectName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Need to add docker registry for generate
            application.Registry = new ContainerRegistry("test", null);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{projectName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);

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
        public async Task GenerateWorksWithRegistryPullSecret()
        {
            await DockerAssert.DeleteDockerImagesAsync(output, "test/test-project");

            var projectName = "single-project";
            var environment = "production";

            using var projectDirectory = CopyTestProjectDirectory("single-project");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Need to add docker registry with pull secret for generate
            application.Registry = new ContainerRegistry("test", "credsecret");

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{projectName}-registrypullsecret.yaml");

                YamlAssert.Equals(expectedContent, content, output);

                await DockerAssert.AssertImageExistsAsync(output, "test/test-project");
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, "test/test-project");
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task GenerateWorksWithoutRegistry()
        {
            await DockerAssert.DeleteDockerImagesAsync(output, "test/test-project");

            var projectName = "single-project";
            var environment = "production";

            using var projectDirectory = CopyTestProjectDirectory(projectName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            // Need to add docker registry for generate
            application.Registry = new ContainerRegistry("test", null);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{projectName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);

                await DockerAssert.AssertImageExistsAsync(output, "test/test-project");
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, "test/test-project");
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task Generate_DaprApplication()
        {
            var applicationName = "dapr_test_application";
            var projectName = "dapr-test-project";
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

                YamlAssert.Equals(expectedContent, content, output);

                await DockerAssert.AssertImageExistsAsync(output, projectName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, projectName);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task Generate_ConnectionStringDependency()
        {
            var applicationName = "generate-connectionstring-dependency";
            var projectName = "frontend";
            var environment = "production";

            await DockerAssert.DeleteDockerImagesAsync(output, projectName);

            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(applicationName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{applicationName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{applicationName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);

                await DockerAssert.AssertImageExistsAsync(output, projectName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, projectName);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task Generate_UriDependency()
        {
            var applicationName = "generate-uri-dependency";
            var projectName = "frontend";
            var environment = "production";

            await DockerAssert.DeleteDockerImagesAsync(output, projectName);

            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(applicationName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{applicationName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{applicationName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);

                await DockerAssert.AssertImageExistsAsync(output, projectName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, projectName);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task Generate_NamedBinding()
        {
            var applicationName = "generate-named-binding";
            var projectName = "frontend";
            var environment = "production";

            await DockerAssert.DeleteDockerImagesAsync(output, projectName);

            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(applicationName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{applicationName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{applicationName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);

                await DockerAssert.AssertImageExistsAsync(output, projectName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, projectName);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task Generate_DirectDependencyForEnvVars()
        {
            var applicationName = "multirepo";
            var projectName = "results";
            var otherProject = "worker";
            var environment = "production";

            await DockerAssert.DeleteDockerImagesAsync(output, projectName);
            await DockerAssert.DeleteDockerImagesAsync(output, otherProject);

            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(applicationName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "results", "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, "results", $"VotingSample-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{applicationName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);
                await DockerAssert.AssertImageExistsAsync(output, projectName);
                await DockerAssert.AssertImageExistsAsync(output, otherProject);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, projectName);
                await DockerAssert.DeleteDockerImagesAsync(output, otherProject);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task Generate_Ingress()
        {
            var applicationName = "apps-with-ingress";
            var environment = "production";

            await DockerAssert.DeleteDockerImagesAsync(output, "appa");
            await DockerAssert.DeleteDockerImagesAsync(output, "appa");

            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(applicationName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{applicationName}-generate-{environment}.yaml"));

                if (await KubectlDetector.GetKubernetesServerVersion(outputContext) >= new Version(1, 19))
                {
                    var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{applicationName}.1.19.yaml");
                    YamlAssert.Equals(expectedContent, content, output);
                }
                else
                {
                    var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{applicationName}.1.18.yaml");
                    YamlAssert.Equals(expectedContent, content, output);
                }

                await DockerAssert.AssertImageExistsAsync(output, "appa");
                await DockerAssert.AssertImageExistsAsync(output, "appb");
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, "appa");
                await DockerAssert.DeleteDockerImagesAsync(output, "appb");
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task Generate_HealthChecks()
        {
            var applicationName = "health-checks";
            var environment = "production";
            var projectName = "health-all";

            await DockerAssert.DeleteDockerImagesAsync(output, projectName);

            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(applicationName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye-all.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{applicationName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{applicationName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);

                await DockerAssert.AssertImageExistsAsync(output, projectName);
            }
            finally
            {
                await DockerAssert.DeleteDockerImagesAsync(output, projectName);
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task Generate_DockerFile()
        {
            var applicationName = "dockerfile";
            var environment = "production";
            var projectName = "frontend-backend";

            await DockerAssert.DeleteDockerImagesAsync(output, "frontend");
            await DockerAssert.DeleteDockerImagesAsync(output, "backend");

            using var projectDirectory = TestHelpers.CopyTestProjectDirectory(applicationName);

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);

            try
            {
                await GenerateHost.ExecuteGenerateAsync(outputContext, application, environment, interactive: false);

                // name of application is the folder
                var content = await File.ReadAllTextAsync(Path.Combine(projectDirectory.DirectoryPath, $"{projectName}-generate-{environment}.yaml"));
                var expectedContent = await File.ReadAllTextAsync($"testassets/generate/{applicationName}.yaml");

                YamlAssert.Equals(expectedContent, content, output);

                await DockerAssert.AssertImageExistsAsync(output, "frontend");
                await DockerAssert.AssertImageExistsAsync(output, "backend");
            }
            finally
            {

                await DockerAssert.DeleteDockerImagesAsync(output, "frontend");
                await DockerAssert.DeleteDockerImagesAsync(output, "backend");
            }
        }
    }
}
