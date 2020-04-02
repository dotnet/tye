// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Tye;
using Xunit;
using Xunit.Abstractions;
using static E2ETest.TestHelpers;

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
            using var projectDirectory = CopySampleProjectDirectory(Path.Combine("single-project", "test-project"));

            File.Delete(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "test-project.csproj"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);
            var expectedContent = File.ReadAllText("testassets/init/single-project.yaml");

            output.WriteLine(content);

            Assert.Equal(expectedContent.NormalizeNewLines(), content.NormalizeNewLines());
        }

        [Fact]
        public void MultiProjectInitTest()
        {
            using var projectDirectory = CopySampleProjectDirectory("multi-project");

            // delete already present yaml
            File.Delete(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "multi-project.sln"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);
            var expectedContent = File.ReadAllText("testassets/init/multi-project.yaml");

            output.WriteLine(content);

            Assert.Equal(expectedContent.NormalizeNewLines(), content.NormalizeNewLines());
        }

        [Fact]
        public void FrontendBackendTest()
        {
            using var projectDirectory = CopySampleProjectDirectory("frontend-backend");

            // delete already present yaml
            File.Delete(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "frontend-backend.sln"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);
            var expectedContent = File.ReadAllText("testassets/init/frontend-backend.yaml");

            output.WriteLine(content);

            Assert.Equal(expectedContent.NormalizeNewLines(), content.NormalizeNewLines());
        }
    }
}
