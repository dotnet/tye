// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.Build.Construction;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tye.ConfigModel
{
    public static class ConfigFactory
    {
        public static ConfigApplication FromFile(FileInfo file)
        {
            var extension = file.Extension.ToLowerInvariant();
            switch (extension)
            {
                case ".yaml":
                case ".yml":
                    return FromYaml(file);

                case ".csproj":
                case ".fsproj":
                    return FromProject(file);

                case ".sln":
                    return FromSolution(file);

                default:
                    throw new CommandException($"File '{file.FullName}' is not a supported format.");
            }
        }

        private static ConfigApplication FromProject(FileInfo file)
        {
            var application = new ConfigApplication()
            {
                Source = file,
            };

            var service = CreateService(file);
            if (service is object)
            {
                application.Services.Add(service);
            }

            return application;
        }

        private static ConfigApplication FromSolution(FileInfo file)
        {
            var application = new ConfigApplication()
            {
                Source = file,
            };

            var solution = SolutionFile.Parse(file.FullName);
            foreach (var project in solution.ProjectsInOrder)
            {
                if (project.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat)
                {
                    continue;
                }

                var projectFilePath = project.AbsolutePath.Replace('\\', Path.DirectorySeparatorChar);
                var extension = Path.GetExtension(projectFilePath).ToLower();
                switch (extension)
                {
                    case ".csproj":
                    case ".fsproj":
                        break;
                    default:
                        continue;
                }

                var description = CreateService(new FileInfo(projectFilePath));
                if (description != null)
                {
                    application.Services.Add(description);
                }
            }

            return application;
        }

        private static ConfigApplication FromYaml(FileInfo file)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            using var reader = file.OpenText();
            var application = deserializer.Deserialize<ConfigApplication>(reader);
            application.Source = file;

            foreach (var service in application.Services)
            {
                if (service.Project == null)
                {
                    continue;
                }

                if (!TryGetLaunchProfile(new FileInfo(Path.Combine(file.DirectoryName, service.Project)), out var launchProfile))
                {
                    continue;
                }

                PopulateFromLaunchProfile(service, launchProfile);
            }

            return application;
        }

        private static bool TryGetLaunchProfile(FileInfo file, out JsonElement launchProfile)
        {
            var launchSettingsPath = Path.Combine(file.DirectoryName, "Properties", "launchSettings.json");
            if (!File.Exists(launchSettingsPath))
            {
                launchProfile = default;
                return false;
            }

            // If there's a launchSettings.json, then use it to get addresses
            var root = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(launchSettingsPath));
            var key = NameSanitizer.SanitizeToIdentifier(Path.GetFileNameWithoutExtension(file.Name));
            var profiles = root.GetProperty("profiles");
            return profiles.TryGetProperty(key, out launchProfile);
        }

        private static ConfigService? CreateService(FileInfo file)
        {
            if (!TryGetLaunchProfile(file, out var launchProfile))
            {
                return null;
            }

            var service = new ConfigService()
            {
                Name = Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant(),
                Project = file.FullName,
            };

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
