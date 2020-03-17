// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.Tye
{
    public static class TargetInstaller
    {
        public static void Install(string projectFilePath)
        {
            if (projectFilePath is null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }

            var projectDirectory = Path.GetDirectoryName(projectFilePath);
            var intermediateDirectory = Path.Combine(projectDirectory!, "obj");

            Directory.CreateDirectory(intermediateDirectory);

            var fileName = $"{Path.GetFileName(projectFilePath)}.Tye.targets";
            var targetFilePath = Path.Combine(intermediateDirectory, fileName);

            if (File.Exists(targetFilePath))
            {
                return;
            }

            var toolType = typeof(TargetInstaller);
            var toolAssembly = toolType.GetTypeInfo().Assembly;
            var toolImportTargetsResourceName = $"Tye.Resources.Imports.targets";

            using var stream = toolAssembly.GetManifestResourceStream(toolImportTargetsResourceName);
            if (stream == null)
            {
                throw new CommandException("Failed to find resource. Valid names: " + string.Join(", ", toolAssembly.GetManifestResourceNames()));
            }

            var targetBytes = new byte[stream.Length];
            stream.Read(targetBytes, 0, targetBytes.Length);
            File.WriteAllBytes(targetFilePath, targetBytes);
        }
    }
}
