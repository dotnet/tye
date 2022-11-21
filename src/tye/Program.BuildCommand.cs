// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;

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
                StandardOptions.Tags,
                StandardOptions.Verbosity,
                StandardOptions.Framework,
                StandardOptions.Environment,
            };

            command.Handler = CommandHandler.Create<BuildCommandArguments>(args =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (args.Path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(args.Console, args.Verbosity);
                output.WriteInfoLine("Loading Application Details...");

                var filter = ApplicationFactoryFilter.GetApplicationFactoryFilter(args.Tags);

                return BuildHost.BuildAsync(output, args.Path, args.Interactive, args.Environment, args.Framework, filter);
            });

            return command;
        }

        private class BuildCommandArguments
        {
            public IConsole Console { get; set; } = default!;

            public FileInfo Path { get; set; } = default!;

            public Verbosity Verbosity { get; set; }

            public string Framework { get; set; } = default!;

            public bool Interactive { get; set; } = false;

            public string[] Tags { get; set; } = Array.Empty<string>();

            public string Environment { get; set; } = default!;
        }
    }
}
