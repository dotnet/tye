// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Tye;
using Xunit;

namespace E2ETest
{
    public static class TestHelpers
    {
        // https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/Testing/src/TestPathUtilities.cs
        // This can get into a bad pattern for having crazy paths in places. Eventually, especially if we use helix,
        // we may want to avoid relying on sln position.
        public static string GetSolutionRootDirectory(string solution)
        {
            var applicationBasePath = AppContext.BaseDirectory;
            var directoryInfo = new DirectoryInfo(applicationBasePath);

            do
            {
                var projectFileInfo = new FileInfo(Path.Combine(directoryInfo.FullName, $"{solution}.sln"));
                if (projectFileInfo.Exists)
                {
                    return projectFileInfo.DirectoryName;
                }

                directoryInfo = directoryInfo.Parent;
            }
            while (directoryInfo.Parent != null);

            throw new Exception($"Solution file {solution}.sln could not be found in {applicationBasePath} or its parent directories.");
        }

        public static DirectoryInfo GetTestAssetsDirectory()
        {
            return new DirectoryInfo(Path.Combine(
                TestHelpers.GetSolutionRootDirectory("tye"),
                "test",
                "E2ETest",
                "testassets"));
        }

        public static DirectoryInfo GetTestProjectDirectory(string projectName)
        {
            var directory = new DirectoryInfo(Path.Combine(
                TestHelpers.GetSolutionRootDirectory("tye"),
                "test",
                "E2ETest",
                "testassets",
                "projects",
                projectName));
            Assert.True(directory.Exists, $"Project {projectName} not found.");
            return directory;
        }

        internal static TempDirectory CopyTestProjectDirectory(string projectName)
        {
            var temp = TempDirectory.Create();
            DirectoryCopy.Copy(GetTestProjectDirectory(projectName).FullName, temp.DirectoryPath);
            return temp;
        }
    }
}
