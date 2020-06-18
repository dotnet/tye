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
        public static Command CreateUndeployCommand()
        {
            var command = new Command("undeploy", "delete deployed application")
            {
                CommonArguments.Path_Required,
                StandardOptions.Namespace,
                StandardOptions.Interactive,
                StandardOptions.Verbosity,
                StandardOptions.Tags,

                new Option(new[]{ "--what-if", }, "print what would be deleted without making changes")
                {
                    Argument = new Argument<bool>(),
                },
            };

            command.Handler = CommandHandler.Create<IConsole, FileInfo, Verbosity, string, bool, bool, string[]>((console, path, verbosity, @namespace, interactive, whatIf, tags) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                return UndeployHost.UndeployAsync(console, path, verbosity, @namespace, interactive, whatIf, tags);
            });

            return command;
        }
    }
}
