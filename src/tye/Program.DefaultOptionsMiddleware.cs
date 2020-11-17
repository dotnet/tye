// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public static void DefaultOptionsMiddleware(InvocationContext context)
        {
            // Check if current command is child of root command - default options for deeper nested commands is not supported at the moment
            if (context.ParseResult.CommandResult.Parent != context.ParseResult.RootCommandResult)
            {
                return;
            }

            var commandName = context.ParseResult.CommandResult.Command.Name;

            // Get default options from environment variable for current command
            var rawDefaultOptions = Environment.GetEnvironmentVariable(DefaultOptionsEnvVarPrefix + commandName.ToUpper() + DefaultOptionsEnvVarPostfix);
            if (string.IsNullOrWhiteSpace(rawDefaultOptions))
            {
                return;
            }

            var originalParseResult = context.ParseResult;
            // Get currently applied options
            var originalOptionResults = GetCommandOptions(originalParseResult.CommandResult);
            // Recreate orignial input
            var originalTokens = StringifyTokens(originalParseResult.Tokens);

            // Exit early if --no-default option is applied
            if (originalOptionResults.Any(option => option.Option.Name == StandardOptions.NoDefaultOptions.Name))
            {
                return;
            }

            // Parse default options to validate them
            var defaultParseResult = context.Parser.Parse($"{commandName} {rawDefaultOptions}");
            // Get valid default options
            var defaultOptionResults = GetCommandOptions(defaultParseResult.CommandResult);
            // Get only options that are not already applied
            var additionalTokens = GetAdditionalOptionsTokens(originalOptionResults, defaultOptionResults);

            // Set parse result as combination of original input plus default options
            context.ParseResult = context.Parser.Parse($"{originalTokens} {additionalTokens}");

            static string StringifyTokens(IEnumerable<Token> tokens)
            {
                return string.Join(" ", tokens.Select(t => t.Value));
            }

            static IEnumerable<OptionResult> GetCommandOptions(CommandResult commandResult)
            {
                return commandResult.Children.OfType<OptionResult>();
            }

            // Filter only options which are not already applied in original command or which are implicit
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
