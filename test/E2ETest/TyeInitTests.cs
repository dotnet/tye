﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Tye;
using Microsoft.Tye.ConfigModel;
using Test.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static Test.Infrastructure.TestHelpers;

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
        public void Init_WorksForMultipleProjects()
        {
            using var projectDirectory = CopyTestProjectDirectory("multi-project");
            File.Delete(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "multi-project.sln"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);

            var expectedContent = File.ReadAllText("testassets/init/multi-project.yaml");

            YamlAssert.Equals(expectedContent, content);
        }

        [Fact]
        public void Init_WorksForMultipleProjects_FileComparison()
        {
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

            // delete already present yaml
            File.Delete(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "frontend-backend.sln"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);
            var expectedContent = File.ReadAllText("testassets/init/frontend-backend.yaml");

            output.WriteLine(content);

            YamlAssert.Equals(expectedContent, content);
        }

        // Tests our logic that excludes non-applications (unit tests, classlibs, etc)
        [Fact]
        public void Init_ProjectKinds()
        {
            using var projectDirectory = CopyTestProjectDirectory("project-types");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "project-types.sln"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);

            var expectedContent = File.ReadAllText("testassets/init/project-types.yaml");

            YamlAssert.Equals(expectedContent, content);
        }


        [Fact]
        public void Console_Normalization_Service_Name()
        {
            using var projectDirectory = CopyTestProjectDirectory("Console.Normalization.svc.Name");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "Console.Normalization.svc.Name.csproj"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);

            var expectedContent = File.ReadAllText("testassets/init/console-normalization-svc-name.yaml");

            YamlAssert.Equals(expectedContent, content);
        }

        [Fact]
        public void Tye_Init_Force_Works()
        {
            using var projectDirectory = CopyTestProjectDirectory("single-project");
            var tyePath = Path.Combine(projectDirectory.DirectoryPath, "tye.yaml");
            var text = File.ReadAllText(tyePath);
            File.WriteAllText(tyePath, text + "thisisatest");

            var projectFile = new FileInfo(tyePath);

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: true);

            Assert.DoesNotContain("thisisatest", content);
        }
    }
}
