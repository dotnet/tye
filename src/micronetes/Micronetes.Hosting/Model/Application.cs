using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Build.Construction;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Micronetes.Hosting.Model
{
    public class Application
    {
        public string ContextDirectory { get; set; } = Directory.GetCurrentDirectory();

        public string Source { get; set; }

        public Application(IEnumerable<ServiceDescription> services)
        {
            var map = new Dictionary<string, Service>();

            // TODO: Do validation here
            foreach (var s in services)
            {
                s.Replicas ??= 1;
                map[s.Name] = new Service { Description = s };
            }

            Services = map;
        }

        public static Application FromYaml(string path)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var descriptions = deserializer.Deserialize<ServiceDescription[]>(new StringReader(File.ReadAllText(path)));

            var contextDirectory = Path.GetDirectoryName(fullPath);

            foreach (var d in descriptions)
            {
                if (d.Project == null)
                {
                    continue;
                }

                // Try to populate more from launch settings
                var projectFilePath = Path.GetFullPath(Path.Combine(contextDirectory, d.Project));

                if (!TryGetLaunchSettings(projectFilePath, out var projectSettings))
                {
                    continue;
                }

                PopulateFromLaunchSettings(d, projectSettings);
            }

            return new Application(descriptions)
            {
                Source = fullPath,
                // Use the file location as the context when loading from a file
                ContextDirectory = contextDirectory
            };
        }

        public static Application FromProject(string path)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));

            var projectDescription = CreateDescriptionFromProject(fullPath);

            return new Application(projectDescription == null ? new ServiceDescription[0] : new ServiceDescription[] { projectDescription })
            {
                Source = fullPath,
                ContextDirectory = Path.GetDirectoryName(fullPath)
            };
        }

        private static ServiceDescription CreateDescriptionFromProject(string fullPath)
        {
            if (!TryGetLaunchSettings(fullPath, out var projectSettings))
            {
                return null;
            }

            var projectDescription = new ServiceDescription
            {
                Name = Path.GetFileNameWithoutExtension(fullPath).ToLower(),
                Project = fullPath
            };

            PopulateFromLaunchSettings(projectDescription, projectSettings);

            return projectDescription;
        }

        private static void PopulateFromLaunchSettings(ServiceDescription projectDescription, JsonElement projectSettings)
        {
            if (projectDescription.Bindings.Count == 0 && projectSettings.TryGetProperty("applicationUrl", out var applicationUrls))
            {
                var addresses = applicationUrls.GetString()?.Split(';');

                foreach (var address in addresses)
                {
                    var uri = new Uri(address);

                    projectDescription.Bindings.Add(new ServiceBinding
                    {
                        Port = uri.Port,
                        Protocol = uri.Scheme
                    });
                }
            }

            if (projectDescription.Configuration.Count == 0 && projectSettings.TryGetProperty("environmentVariables", out var environmentVariables))
            {
                foreach (var envVar in environmentVariables.EnumerateObject())
                {
                    projectDescription.Configuration.Add(new ConfigurationSource
                    {
                        Name = envVar.Name,
                        Value = envVar.Value.GetString()
                    });
                }
            }

            if (projectDescription.Replicas == null && projectSettings.TryGetProperty("replicas", out var replicasElement))
            {
                projectDescription.Replicas = replicasElement.GetInt32();
            }
        }

        public static Application FromSolution(string path)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));

            var solution = SolutionFile.Parse(fullPath);

            var descriptions = new List<ServiceDescription>();

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

                var description = CreateDescriptionFromProject(projectFilePath);

                if (description != null)
                {
                    descriptions.Add(description);
                }
            }

            return new Application(descriptions)
            {
                Source = fullPath,
                ContextDirectory = Path.GetDirectoryName(fullPath)
            };
        }

        public Dictionary<string, Service> Services { get; }

        internal void PopulateEnvironment(Service service, Action<string, string> set, string defaultHost = "localhost")
        {
            if (service.Description.Configuration != null)
            {
                // Inject normal configuration
                foreach (var pair in service.Description.Configuration)
                {
                    set(pair.Name, pair.Value);
                }
            }

            void SetBinding(string serviceName, ServiceBinding b)
            {
                var configName = "";
                var envName = "";

                if (string.IsNullOrEmpty(b.Name))
                {
                    configName = serviceName;
                    envName = serviceName;
                }
                else
                {
                    configName = $"{serviceName.ToUpper()}__{b.Name.ToUpper()}";
                    envName = $"{serviceName.ToUpper()}_{b.Name.ToUpper()}";
                }

                if (!string.IsNullOrEmpty(b.ConnectionString))
                {
                    // Special case for connection strings
                    set($"CONNECTIONSTRING__{configName}", b.ConnectionString);
                }

                if (!string.IsNullOrEmpty(b.Protocol))
                {
                    // IConfiguration specific (double underscore ends up telling the configuration provider to use it as a separator)
                    set($"SERVICE__{configName}__PROTOCOL", b.Protocol);
                    set($"{envName}_SERVICE_PROTOCOL", b.Protocol);
                }

                if (b.Port != null)
                {
                    set($"SERVICE__{configName}__PORT", b.Port.ToString());
                    set($"{envName}_SERVICE_PORT", b.Port.ToString());
                }

                set($"SERVICE__{configName}__HOST", b.Host ?? defaultHost);
                set($"{envName}_SERVICE_HOST", b.Host ?? defaultHost);
            }

            // Inject dependency information
            foreach (var s in Services.Values)
            {
                foreach (var b in s.Description.Bindings)
                {
                    SetBinding(s.Description.Name.ToUpper(), b);
                }
            }
        }

        private static bool TryGetLaunchSettings(string projectFilePath, out JsonElement projectSettings)
        {
            var projectDirectory = Path.GetDirectoryName(projectFilePath);
            var launchSettingsPath = Path.Combine(projectDirectory, "Properties", "launchSettings.json");

            if (!File.Exists(launchSettingsPath))
            {
                projectSettings = default;
                return false;
            }

            // If there's a launchSettings.json, then use it to get addresses
            var root = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(launchSettingsPath));
            var key = Path.GetFileNameWithoutExtension(projectFilePath);
            var profiles = root.GetProperty("profiles");
            return profiles.TryGetProperty(key, out projectSettings);
        }
    }
}
