// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Tye.Serialization;

namespace Microsoft.Tye.ConfigModel
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

            var service = new ConfigService()
            {
                Name = Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant(),
                Project = file.FullName.Replace('\\', '/'),
            };

            application.Services.Add(service);

            return application;
        }

        private static ConfigApplication FromSolution(FileInfo file)
        {
            var application = new ConfigApplication()
            {
                Source = file,
            };

            // BE CAREFUL modifying this code. Avoid proliferating MSBuild types
            // throughout the code, because we load them dynamically.
            foreach (var projectFile in ProjectReader.EnumerateProjects(file))
            {
                var service = new ConfigService()
                {
                    Name = Path.GetFileNameWithoutExtension(projectFile.Name).ToLowerInvariant(),
                    Project = projectFile.FullName.Replace('\\', '/'),
                };

                application.Services.Add(service);
            }

            return application;
        }

        private static ConfigApplication FromYaml(FileInfo file)
        {
            var deserializer = YamlSerializer.CreateDeserializer();

            using var reader = file.OpenText();
            var application = deserializer.Deserialize<ConfigApplication>(reader);
            application.Source = file;

            // Deserialization makes all collection properties null so make sure they are non-null so
            // other code doesn't need to react
            foreach (var service in application.Services)
            {
                service.Bindings ??= new List<ConfigServiceBinding>();
                service.Configuration ??= new List<ConfigConfigurationSource>();
                service.Volumes ??= new List<ConfigVolume>();
            }

            foreach (var ingress in application.Ingress)
            {
                ingress.Bindings ??= new List<ConfigIngressBinding>();
                ingress.Rules ??= new List<ConfigIngressRule>();
            }

            return application;
        }
    }
}
