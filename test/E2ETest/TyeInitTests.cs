// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.Build.Construction;
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

            var application = new ConfigApplication()
            {
                Source = projectFile,
            };

            var solution = SolutionFile.Parse(projectFile.FullName);
            foreach (var project in solution.ProjectsInOrder)
            {
                if (project.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat)
                {
                    continue;
                }

                var extension = Path.GetExtension(project.AbsolutePath).ToLower();
                switch (extension)
                {
                    case ".csproj":
                    case ".fsproj":
                        break;
                    default:
                        continue;
                }

                var description = CreateService(new FileInfo(project.AbsolutePath));
                if (description != null)
                {
                    output.WriteLine("Adding description");
                    application.Services.Add(description);
                }
            }

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


        private bool TryGetLaunchProfile(FileInfo file, out JsonElement launchProfile)
        {
            var launchSettingsPath = Path.Combine(file.DirectoryName, "Properties", "launchSettings.json");
            if (!File.Exists(launchSettingsPath))
            {
                output.WriteLine(launchSettingsPath);
                launchProfile = default;
                return false;
            }

            output.WriteLine("Found: " + launchSettingsPath);

            // If there's a launchSettings.json, then use it to get addresses
            var root = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(launchSettingsPath));
            var key = SanitizeToIdentifier(Path.GetFileNameWithoutExtension(file.Name));
            var profiles = root.GetProperty("profiles");
            return profiles.TryGetProperty(key, out launchProfile);
        }

        public static string SanitizeToIdentifier(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            // This is not perfect. For now it just handles cases we've encountered.
            return name.Replace("-", "_");
        }

        private ConfigService? CreateService(FileInfo file)
        {
            output.WriteLine(file.FullName);
            if (!TryGetLaunchProfile(file, out var launchProfile))
            {
                output.WriteLine("No launch profile");
                return null;
            }

            var service = new ConfigService()
            {
                Name = Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant(),
                Project = file.FullName.Replace('\\', '/'),
            };

            output.WriteLine(service.Name);
            output.WriteLine(service.Project);

            PopulateFromLaunchProfile(service, launchProfile);

            return service;
        }

        private static void PopulateFromLaunchProfile(ConfigService service, JsonElement launchProfile)
        {
            if (service.Bindings.Count == 0 && launchProfile.TryGetProperty("applicationUrl", out var applicationUrls))
            {
                var addresses = applicationUrls.GetString().Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var address in addresses)
                {
                    var uri = new Uri(address);
                    service.Bindings.Add(new ConfigServiceBinding()
                    {
                        Port = uri.Port,
                        Protocol = uri.Scheme
                    });
                }
            }

            if (service.Configuration.Count == 0 && launchProfile.TryGetProperty("environmentVariables", out var environmentVariables))
            {
                foreach (var envVar in environmentVariables.EnumerateObject())
                {
                    service.Configuration.Add(new ConfigConfigurationSource()
                    {
                        Name = envVar.Name,
                        Value = envVar.Value.GetString()
                    });
                }
            }
        }
    }
}
