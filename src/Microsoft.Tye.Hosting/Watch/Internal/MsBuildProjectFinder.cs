﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal class MsBuildProjectFinder
    {
        /// <summary>
        /// Finds a compatible MSBuild project.
        /// <param name="searchBase">The base directory to search</param>
        /// <param name="project">The filename of the project. Can be null.</param>
        /// </summary>
        public static string FindMsBuildProject(string searchBase, string project)
        {
            Ensure.NotNullOrEmpty(searchBase, nameof(searchBase));

            var projectPath = project ?? searchBase;

            if (!Path.IsPathRooted(projectPath))
            {
                projectPath = Path.Combine(searchBase, projectPath);
            }

            if (Directory.Exists(projectPath))
            {
                var projects = Directory.EnumerateFileSystemEntries(projectPath, "*.*proj", SearchOption.TopDirectoryOnly)
                    .Where(f => !".xproj".Equals(Path.GetExtension(f), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (projects.Count > 1)
                {
                    throw new FileNotFoundException($"The project file '{projectPath}' does not exist.");
                }

                if (projects.Count == 0)
                {
                    throw new FileNotFoundException($"Could not find a MSBuild project file in '{projectPath}'. Specify which project to use with the --project option.");
                }

                return projects[0];
            }

            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException($"The project file '{projectPath}' does not exist.");
            }

            return projectPath;
        }
    }
}
