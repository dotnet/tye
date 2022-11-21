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
        public static Command CreateGenerateCommand()
        {
            var command = new Command("generate", "generate kubernetes manifests")
            {
                CommonArguments.Path_Required,
                StandardOptions.Interactive,
                StandardOptions.Verbosity,
                StandardOptions.Namespace,
                StandardOptions.Tags,
                StandardOptions.Framework,
                StandardOptions.Environment,
            };

            // This is a super-secret VIP-only command! It's useful for testing, but we're 
            // not documenting it right now.
            command.IsHidden = true;

            command.Handler = CommandHandler.Create<GenerateCommandArguments>(args =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (args.Path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(args.Console, args.Verbosity);
                output.WriteInfoLine("Loading Application Details...");

                var filter = ApplicationFactoryFilter.GetApplicationFactoryFilter(args.Tags);

                return GenerateHost.GenerateAsync(output, args.Path, args.Interactive, args.Namespace, args.Environment, args.Framework, filter);
            });

            return command;
        }

        private class GenerateCommandArguments
        {
            public IConsole Console { get; set; } = default!;

            public FileInfo Path { get; set; } = default!;

            public Verbosity Verbosity { get; set; }

            public bool Interactive { get; set; } = false;

            public string Namespace { get; set; } = default!;

            public string Framework { get; set; } = default!;

            public string Environment { get; set; } = default!;

            public string[] Tags { get; set; } = Array.Empty<string>();
        }
    }
}
