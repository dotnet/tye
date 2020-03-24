// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Tye.ConfigModel;

namespace Microsoft.Tye
{
    static partial class Program
    {
        public static Command CreateBuildCommand()
        {
            var command = new Command("build", "build containers for the application")
            {
                CommonArguments.Path_Required,
                StandardOptions.Interactive,
                StandardOptions.Verbosity,
            };

            command.Handler = CommandHandler.Create<IConsole, FileInfo, Verbosity, bool>((console, path, verbosity, interactive) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                return BuildHost.BuildAsync(console, path, verbosity, interactive);
            });

            return command;
        }
    }
}
