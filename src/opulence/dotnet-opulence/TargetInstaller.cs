// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;

namespace Opulence
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

            var fileName = $"{Path.GetFileName(projectFilePath)}.opulence.targets";
            var targetFilePath = Path.Combine(intermediateDirectory, fileName);

            if (File.Exists(targetFilePath))
            {
                return;
            }

            var toolType = typeof(TargetInstaller);
            var toolAssembly = toolType.GetTypeInfo().Assembly;
            var toolImportTargetsResourceName = $"Opulence.Resources.Imports.targets";

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
