// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Tye;
using Tye.ConfigModel;
using Xunit;
using Xunit.Abstractions;

namespace E2ETest
{
    public class TyeInitTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestOutputLogEventSink sink;

        public TyeInitTests(ITestOutputHelper output)
        {
            this.output = output;
            sink = new TestOutputLogEventSink(output);
        }

        [Fact]
        public void SingleProjectInitTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "single-project", "test-project"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "test-project.csproj"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);
            var expectedContent = File.ReadAllText("testassets/init/single-project.yaml");

            Assert.Equal(expectedContent, content);
        }

        [Fact]
        public void MultiProjectInitTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "multi-project"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            // delete already present yaml
            File.Delete(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "multi-project.sln"));
            output.WriteLine(projectFile.Exists.ToString());
            var directory = new DirectoryInfo(tempDirectory.DirectoryPath);

            var application = ConfigFactory.FromFile(projectFile);

            output.WriteLine(application.Services.Count.ToString());
            foreach (var service in application.Services)
            {
                output.WriteLine(service.Name);
                output.WriteLine(service.Project);
            }

            foreach (var file in directory.GetFiles())
            {
                output.WriteLine(file.FullName);
            }

            foreach (var file in directory.GetDirectories())
            {
                output.WriteLine(file.FullName);
            }
            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);
            var expectedContent = File.ReadAllText("testassets/init/multi-project.yaml");

            output.WriteLine(content);

            Assert.Equal(expectedContent, content);
        }

        [Fact]
        public void FrontendBackendTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "frontend-backend"));
            using var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            // delete already present yaml
            File.Delete(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "frontend-backend.sln"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);
            var expectedContent = File.ReadAllText("testassets/init/frontend-backend.yaml");

            output.WriteLine(content);

            Assert.Equal(expectedContent, content);
        }
    }
}
