// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO;
using Microsoft.Tye.ConfigModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.Tye
{
    static partial class Program
    {
        private static Command CreateInitCommand()
        {
            var command = new Command("init", "create a yaml manifest")
            {
                CommonArguments.Path_Optional,
            };

            command.AddOption(new Option(new[] { "-f", "--force" })
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
