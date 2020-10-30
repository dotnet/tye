using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;

namespace Microsoft.Tye
{
    public static partial class Program
    {
        private const string DefaultOptionsEnvVarPrefix = "TYE_";
        private const string DefaultOptionsEnvVarPostfix = "_ARGS";

        private static void DefaultOptionsMiddleware(InvocationContext context)
        {
            if (context.ParseResult.CommandResult.Parent != context.ParseResult.RootCommandResult)
            {
                return;
            }

            var commandName = context.ParseResult.CommandResult.Command.Name;

            var rawDefaultOptions = Environment.GetEnvironmentVariable(DefaultOptionsEnvVarPrefix + commandName.ToUpper() + DefaultOptionsEnvVarPostfix);
            if (string.IsNullOrWhiteSpace(rawDefaultOptions))
            {
                return;
            }

            var originalParseResult = context.ParseResult;
            var originalOptionResults = GetCommandOptions(originalParseResult.CommandResult);
            var originalTokens = StringifyTokens(originalParseResult.Tokens);

            if (originalOptionResults.Any(option => option.Option.Name == StandardOptions.NoDefaultOptions.Name))
            {
                return;
            }

            var defaultParseResult = context.Parser.Parse($"{commandName} {rawDefaultOptions}");
            var defaultOptionResults = GetCommandOptions(defaultParseResult.CommandResult);
            var additionalTokens = GetAdditionalOptionsTokens(originalOptionResults, defaultOptionResults);

            context.ParseResult = context.Parser.Parse($"{originalTokens} {additionalTokens}");

            static string StringifyTokens(IEnumerable<Token> tokens)
            {
                return string.Join(" ", tokens.Select(t => t.Value));
            }

            static IEnumerable<OptionResult> GetCommandOptions(CommandResult commandResult)
            {
                return commandResult.Children.OfType<OptionResult>();
            }

            static string GetAdditionalOptionsTokens(IEnumerable<OptionResult> originalOptions, IEnumerable<OptionResult> defaultOptions)
            {
                var additionalOptions = defaultOptions
                    .Where(@default => !originalOptions.Any(original => !original.IsImplicit && original.Option.Name == @default.Option.Name))
                    .Select(additional => $"{additional.Token.Value} {StringifyTokens(additional.Tokens)}");
                return string.Join(" ", additionalOptions);
            }
        }
    }
}
