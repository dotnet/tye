// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Tye;
using Microsoft.Tye.ConfigModel;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static E2ETest.TestHelpers;

namespace E2ETest
{
    public class TyeInitTests
    {
        private readonly ITestOutputHelper output;
        private readonly TestOutputLogEventSink sink;
        private readonly IDeserializer _deserializer;

        public TyeInitTests(ITestOutputHelper output)
        {
            this.output = output;
            sink = new TestOutputLogEventSink(output);
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        [Fact]
        public void Init_WorksForMultipleProjects()
        {
            using var projectDirectory = CopyTestProjectDirectory("multi-project");
            File.Delete(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "multi-project.sln"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);
            var actual = _deserializer.Deserialize<ConfigApplication>(content);

            var expectedContent = File.ReadAllText("testassets/init/multi-project.yaml");
            var expected = _deserializer.Deserialize<ConfigApplication>(expectedContent);

            CompareConfigApplications(expected, actual);
        }

        // Tests our logic that excludes non-applications (unit tests, classlibs, etc)
        [Fact]
        public void Init_ProjectKinds()
        {
            using var projectDirectory = CopyTestProjectDirectory("project-types");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "project-types.sln"));

            var (content, _) = InitHost.CreateTyeFileContent(projectFile, force: false);
            var actual = _deserializer.Deserialize<ConfigApplication>(content);

            var expectedContent = File.ReadAllText("testassets/init/project-types.yaml");
            var expected = _deserializer.Deserialize<ConfigApplication>(expectedContent);

            CompareConfigApplications(expected, actual);
        }
    }
}
