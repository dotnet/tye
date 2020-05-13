// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Tye.Serialization;
using Tye.Serialization;

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
                Name = Normalization(Path.GetFileNameWithoutExtension(file.Name)),
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
                // Check for the existance of a launchSettings.json as an indication that the project is
                // runnable. This will only apply in the case where tye is being used against a solution
                // like `tye init` or `tye run` without a `tye.yaml`.
                //
                // We want a *fast* heuristic that excludes unit test projects and class libraries without
                // having to load all of the projects. 
                var launchSettings = Path.Combine(projectFile.DirectoryName, "Properties", "launchSettings.json");
                if (File.Exists(launchSettings) || ContainsOutputTypeExe(projectFile))
                {
                    var service = new ConfigService()
                    {
                        Name = Normalization(Path.GetFileNameWithoutExtension(projectFile.Name)),
                        Project = projectFile.FullName.Replace('\\', '/'),
                    };

                    application.Services.Add(service);
                }
            }

            return application;
        }

        private static bool ContainsOutputTypeExe(FileInfo projectFile)
        {
            // Note, this will not work if OutputType is on separate lines.
            // TODO consider a more thorough check with xml reading, but at that point, it may be better just to read the project itself.
            var content = File.ReadAllText(projectFile.FullName);
            return content.Contains("<OutputType>exe</OutputType>", StringComparison.OrdinalIgnoreCase);
        }

        private static ConfigApplication FromYaml(FileInfo file)
        {
            using var parser = new YamlParser(file);
            return parser.ParseConfigApplication();
        }

        private static string Normalization(string name)
            => Regex.Replace(name.ToLowerInvariant(), "[^0-9A-Za-z-]+", "-");
    }
}
