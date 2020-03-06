// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO;
using Tye.ConfigModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tye
{
    static partial class Program
    {
        private static Command CreateInitCommand()
        {
            var command = new Command("init", "create a yaml manifest")
            {
                CommonArguments.Path_Optional,
            };

            command.AddOption(new Option("--force")
            {
                Description = "Overrides the tye.yaml file if already present for project.",
                Required = false
            });

            command.Handler = CommandHandler.Create<IConsole, FileInfo?, bool>((console, path, force) =>
            {
                var outputFilePath = InitHost.CreateTyeFile(path, force);
                console.Out.WriteLine($"Created '{outputFilePath}'.");
            });

            return command;
        }
    }
}
