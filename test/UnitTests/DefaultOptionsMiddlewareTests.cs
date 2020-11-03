using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Linq;
using Xunit;

namespace Microsoft.Tye.UnitTests
{
    public class DefaultOptionsMiddlewareTests
    {
        private readonly TestConsole _console = new TestConsole();
        private readonly Parser _parser;

        public DefaultOptionsMiddlewareTests()
        {
            var command = new Command("xxx")
            {
                Handler = CommandHandler.Create((IConsole console, ParseResult result) =>
                {
                    foreach (var option in result.CommandResult.Children.OfType<OptionResult>())
                    {
                        console.Out.Write(option.Token.Value);
                        var argument = option.Children.OfType<ArgumentResult>().FirstOrDefault();
                        if (argument?.Tokens.Count > 0)
                        {
                            console.Out.Write($":{argument.Tokens[0].Value}");
                        }
                        console.Out.Write(" ");
                    }
                })
            };

            var subcommand = new Command("yyy");
            command.AddCommand(subcommand);

            var originalOption = new Option("--original");
            var defaultOption = new Option("--default");
            var implicitOption = new Option("--implicit")
            {
                Argument = new Argument<bool>(() => false)
                {
                    Arity = ArgumentArity.ExactlyOne,
                },
            };

            var rootCommand = new RootCommand();
            rootCommand.AddGlobalOption(originalOption);
            rootCommand.AddGlobalOption(defaultOption);
            rootCommand.AddGlobalOption(implicitOption);
            rootCommand.AddGlobalOption(StandardOptions.NoDefaultOptions);
            rootCommand.AddCommand(command);

            _parser = new CommandLineBuilder(rootCommand)
                .UseMiddleware(Program.DefaultOptionsMiddleware)
                .Build();
        }

        private string[] OptionsFromConsole => _console.Out.ToString()?.Split(" ", StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

        [Fact]
        public void Should_apply_default_option()
        {
            Environment.SetEnvironmentVariable("TYE_XXX_ARGS", "--default", EnvironmentVariableTarget.Process);

            _parser.Invoke("xxx --original", _console);

            var appliedOptions = OptionsFromConsole;
            Assert.Contains("--default", appliedOptions);
        }

        [Fact]
        public void Should_not_apply_default_option_if_command_is_root_command()
        {
            Environment.SetEnvironmentVariable("TYE_XXX_ARGS", "--default", EnvironmentVariableTarget.Process);

            _parser.Invoke("--original", _console);

            var appliedOptions = OptionsFromConsole;
            Assert.DoesNotContain("--default", appliedOptions);
        }

        [Fact]
        public void Should_not_apply_default_option_if_command_is_not_child_of_root_command()
        {
            Environment.SetEnvironmentVariable("TYE_XXX_ARGS", "--default", EnvironmentVariableTarget.Process);

            _parser.Invoke("xxx yyy --original", _console);

            var appliedOptions = OptionsFromConsole;
            Assert.DoesNotContain("--default", appliedOptions);
        }

        [Fact]
        public void Should_not_apply_default_option_if_env_var_is_empty()
        {
            Environment.SetEnvironmentVariable("TYE_XXX_ARGS", "", EnvironmentVariableTarget.Process);

            _parser.Invoke("xxx --original", _console);

            var appliedOptions = OptionsFromConsole;
            Assert.DoesNotContain("--default", appliedOptions);
        }

        [Fact]
        public void Should_not_apply_default_option_if_it_is_already_applied()
        {
            Environment.SetEnvironmentVariable("TYE_XXX_ARGS", "--default", EnvironmentVariableTarget.Process);

            _parser.Invoke("xxx --original --default", _console);

            var appliedOptions = OptionsFromConsole;
            Assert.Equal(1, appliedOptions.Count(o => o == "--default"));
        }

        [Fact]
        public void Should_apply_default_option_if_it_is_already_implicitly_applied()
        {
            Environment.SetEnvironmentVariable("TYE_XXX_ARGS", "--default --implicit", EnvironmentVariableTarget.Process);

            _parser.Invoke("xxx --original", _console);

            var appliedOptions = OptionsFromConsole;
            Assert.Contains("--implicit:true", appliedOptions);
        }
    }
}
