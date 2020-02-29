using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.CommandLine.Rendering;
using System.Threading.Tasks;

namespace Opulence
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var command = new RootCommand();
            command.AddCommand(InitCommand.Create());
            command.AddCommand(GenerateCommand.Create());
            command.AddCommand(PackageCommand.Create());
            command.AddCommand(PushCommand.Create());
            command.AddCommand(DeployCommand.Create());

            command.Description = "White-Glove Service for .NET and Kubernetes";
            command.Handler = CommandHandler.Create<IHelpBuilder>(help =>
            {
                help.Write(command);
                return 1;
            });

            var builder = new CommandLineBuilder(command);

            // Parsing behavior
            builder.UseHelp();
            builder.UseVersionOption();
            builder.UseDebugDirective();
            builder.UseParseErrorReporting();
            builder.ParseResponseFileAs(ResponseFileHandling.ParseArgsAsSpaceSeparated);
            builder.UsePrefixes(new[] { "-", "--", }); // disable garbage windows conventions

            builder.CancelOnProcessTermination();
            builder.UseExceptionHandler(HandleException);

            // Allow fancy drawing.
            builder.UseAnsiTerminalWhenAvailable();

            var parser = builder.Build();
            return await parser.InvokeAsync(args);
        }

        private static void HandleException(Exception exception, InvocationContext context)
        {
            context.Console.ResetTerminalForegroundColor();
            context.Console.SetTerminalForegroundColor(ConsoleColor.Red);

            if (exception is OperationCanceledException)
            {
                context.Console.Error.WriteLine("Oh dear! Operation canceled.");
            }
            else if (exception is CommandException command)
            {
                context.Console.Error.WriteLine($"Drats! '{context.ParseResult.CommandResult.Command.Name}' failed:");
                context.Console.Error.WriteLine($"\t{command.Message}");

                if (command.InnerException != null)
                {
                    context.Console.Error.WriteLine();
                    context.Console.Error.WriteLine(command.InnerException.ToString());
                }
            }
            else
            {
                context.Console.Error.WriteLine("An unhandled exception has occurred, how unseemly: ");
                context.Console.Error.WriteLine(exception.ToString());
            }

            context.Console.ResetTerminalForegroundColor();

            context.ResultCode = 1;
        }
    }
}
