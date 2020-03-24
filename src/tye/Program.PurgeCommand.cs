// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using Microsoft.Tye.ConfigModel;
using Microsoft.Tye.Hosting;

namespace Microsoft.Tye
{
    static partial class Program
    {
        public static Command CreatePurgeCommand(string[] args)
        {
            var command = new Command("purge", "purges state from previous run")
            {
                CommonArguments.Path_Required
            };

            command.Handler = CommandHandler.Create<IConsole, FileInfo>(async (console, path) =>
            {
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(console, Verbosity.Quiet);
                var application = await ApplicationFactory.CreateAsync(output, path);

                using var host = new TyeHost(application.ToHostingApplication(), args);
                await host.PurgeAsync();
            });

            return command;
        }
    }
}
