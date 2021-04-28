// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Reflection;
using System.Threading.Tasks;
using Tye.Serialization;

namespace Microsoft.Tye
{
    static partial class Program
    {
        public static Task<int> Main(string[] args)
        {
            var command = new RootCommand()
            {
                Description = "Developer tools and publishing for microservices.",
            };
            command.AddGlobalOption(StandardOptions.NoDefaultOptions);

            command.AddCommand(CreateInitCommand());
            command.AddCommand(CreateGenerateCommand());
            command.AddCommand(CreateRunCommand());
            command.AddCommand(CreateBuildCommand());
            command.AddCommand(CreatePushCommand());
            command.AddCommand(CreateDeployCommand());
            command.AddCommand(CreateUndeployCommand());

            // Show commandline help unless a subcommand was used.
            command.Handler = CommandHandler.Create<IHelpBuilder>(help =>
            {
                help.Write(command);
                return 1;
            });

            var builder = new CommandLineBuilder(command);
            builder.UseHelp();
            builder.UseVersionOption();
            builder.UseDebugDirective();
            builder.UseParseErrorReporting();
            builder.ParseResponseFileAs(ResponseFileHandling.ParseArgsAsSpaceSeparated);

            builder.CancelOnProcessTermination();
            builder.UseExceptionHandler(HandleException);

            builder.UseMiddleware(DefaultOptionsMiddleware);

            var parser = builder.Build();
            return parser.InvokeAsync(args);
        }

        private static void HandleException(Exception exception, InvocationContext context)
        {
            var console = context.Console;

            console.ResetTerminalForegroundColor();
            console.SetTerminalForegroundColor(ConsoleColor.Red);

            if (exception is TargetInvocationException { InnerException: { } } tie)
            {
                exception = tie.InnerException;
            }

            if (exception is OperationCanceledException)
            {
                console.Error.WriteLine("Oh dear! Operation canceled.");
            }
            else if (exception is CommandException command)
            {
                console.Error.WriteLine($"Drats! '{context.ParseResult.CommandResult.Command.Name}' failed:");
                console.Error.WriteLine($"\t{command.Message}");

                if (command.InnerException != null)
                {
                    console.Error.WriteLine();
                    console.Error.WriteLine(command.InnerException.ToString());
                }
            }
            else if (exception is TyeYamlException yaml)
            {
                console.Error.WriteLine($"{yaml.Message}");

                if (yaml.InnerException != null)
                {
                    console.Error.WriteLine();
                    console.Error.WriteLine(yaml.InnerException.ToString());
                }
            }
            else
            {
                console.Error.WriteLine("An unhandled exception has occurred, how unseemly: ");
                console.Error.WriteLine(exception.ToString());
            }

            console.ResetTerminalForegroundColor();

            context.ResultCode = 1;
        }
    }
}
