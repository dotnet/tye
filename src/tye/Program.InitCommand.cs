// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
                StandardOptions.CreateForce("Overrides the tye.yaml file if already present for project.")
            };

            command.Handler = CommandHandler.Create<InitCommandArguments>(args =>
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                var output = new OutputContext(args.Console, args.Verbosity);
                var outputFilePath = InitHost.CreateTyeFile(args.Path, args.Force);
                output.WriteInfoLine($"Created '{outputFilePath}'.");

                watch.Stop();
                var elapsedTime = watch.Elapsed;
                output.WriteInfoLine($"Time Elapsed: {elapsedTime.Hours:00}:{elapsedTime.Minutes:00}:{elapsedTime.Seconds:00}:{elapsedTime.Milliseconds / 10:00}");
            });

            return command;
        }

        public class InitCommandArguments
        {
            public IConsole Console { get; set; } = default!;

            public FileInfo Path { get; set; } = default!;

            public Verbosity Verbosity { get; set; }

            public bool Force { get; set; } = false;
        }
    }
}
