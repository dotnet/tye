// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

            command.Handler = CommandHandler.Create<UndeployCommandArguments>(args =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (args.Path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(args.Console, args.Verbosity);
                output.WriteInfoLine("Loading Application Details...");

                var filter = ApplicationFactoryFilter.GetApplicationFactoryFilter(args.Tags);

                return UndeployHost.UndeployAsync(output, args.Path, args.Namespace, args.Interactive, args.WhatIf, filter);
            });

            return command;
        }

        private class UndeployCommandArguments
        {
            public IConsole Console { get; set; } = default!;

            public FileInfo Path { get; set; } = default!;

            public Verbosity Verbosity { get; set; }

            public string Namespace { get; set; } = default!;

            public bool Interactive { get; set; } = false;

            public bool WhatIf { get; set; } = false;

            public string[] Tags { get; set; } = Array.Empty<string>();
        }
    }
}
