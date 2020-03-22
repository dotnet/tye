// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.IO;

namespace Microsoft.Tye
{
    public sealed class OutputContext
    {
        private const int IndentAmount = 4;

        private readonly Stack<StepTracker> steps;
        private int indent;

        public OutputContext(IConsole console, Verbosity verbosity)
        {
            if (console is null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            Console = console;
            Verbosity = verbosity;

            steps = new Stack<StepTracker>();
        }

        private IConsole Console { get; }

        public Verbosity Verbosity { get; }

        public StepTracker BeginStep(string title)
        {
            if (title is null)
            {
                throw new ArgumentNullException(nameof(title));
            }

            WriteInfoLine(title);

            indent += IndentAmount;
            var currentStep = new StepTracker(this, title);
            steps.Push(currentStep);

            return currentStep;
        }

        private void EndStep(StepTracker step)
        {
            if (!object.ReferenceEquals(step, steps.Peek()))
            {
                throw new InvalidOperationException($"Attempting to end a step that isn't running. Currently executing step: {steps.Peek()?.Title}");
            }

            indent -= IndentAmount;
            steps.Pop();

            if (step.Message != null)
            {
                WriteDebugLine(step.Message);
            }
        }

        private void Write(Verbosity verbosity, string message)
        {
            if (Verbosity >= verbosity)
            {
                if (indent > 0)
                {
                    Console.Out.Write(new string(' ', indent));
                }

                Console.Out.Write(message);
            }
        }

        private void WriteLine(Verbosity verbosity, string message)
        {
            if (Verbosity >= verbosity)
            {
                if (indent > 0)
                {
                    Console.Out.Write(new string(' ', indent));
                }

                Console.Out.WriteLine(message);
            }
        }

        public void WriteAlways(string message)
        {
            Write(Verbosity.Info, message);
        }

        public void WriteAlwaysLine(string message)
        {
            WriteLine(Verbosity.Info, message);
        }

        public void WriteInfo(string message)
        {
            Write(Verbosity.Info, message);
        }

        public void WriteInfoLine(string message)
        {
            WriteLine(Verbosity.Info, message);
        }

        public void WriteDebug(string message)
        {
            Write(Verbosity.Debug, message);
        }

        public void WriteDebugLine(string message)
        {
            WriteLine(Verbosity.Debug, message);
        }

        public void WriteCommandLine(string process, string args)
        {
            WriteDebugLine($"> {process} {args}");
        }

        public bool Confirm(string prompt)
        {
            while (true)
            {
                WriteAlways(prompt + " (y/n): ");

                var key = System.Console.ReadKey();
                WriteAlwaysLine(string.Empty);
                if (key.KeyChar == 'y' || key.KeyChar == 'Y')
                {
                    return true;
                }
                else if (key.KeyChar == 'n' || key.KeyChar == 'N')
                {
                    return false;
                }
                else
                {
                    WriteAlwaysLine("Invalid Input.");
                }
            }
        }

        public string Prompt(string prompt, bool allowEmpty = false)
        {
            while (true)
            {
                WriteAlways(prompt + ": ");
                var line = System.Console.ReadLine();

                if (allowEmpty || !string.IsNullOrEmpty(line))
                {
                    return line.Trim();
                }
            }
        }

        public CapturedCommandOutput Capture()
        {
            return new CapturedCommandOutput(this);
        }

        public sealed class CapturedCommandOutput
        {
            private readonly OutputContext output;
            public CapturedCommandOutput(OutputContext output)
            {
                this.output = output;
            }

            public void StdOut(string line)
            {
                if (output.Verbosity >= Verbosity.Debug)
                {
                    output.Console.SetTerminalForegroundColor(ConsoleColor.Gray);
                    output.Console.Out.WriteLine(new string(' ', output.indent + IndentAmount) + line);
                    output.Console.ResetTerminalForegroundColor();
                }
            }

            public void StdErr(string line)
            {
                if (output.Verbosity >= Verbosity.Info)
                {
                    output.Console.SetTerminalForegroundColor(ConsoleColor.Red);
                    output.Console.Out.WriteLine(new string(' ', output.indent + IndentAmount) + line);
                    output.Console.ResetTerminalForegroundColor();
                }
            }
        }

        public class StepTracker : IDisposable
        {
            private readonly OutputContext output;
            private readonly string title;
            private string? message;
            private bool disposed;

            public StepTracker(OutputContext output, string title)
            {
                if (output is null)
                {
                    throw new ArgumentNullException(nameof(output));
                }

                if (title is null)
                {
                    throw new ArgumentNullException(nameof(title));
                }

                this.output = output;
                this.title = title;
            }

            public bool Completed => message != null;

            public string? Message => message;
            public string Title => title;

            public void MarkComplete(string? message = null)
            {
                this.message = message ?? ("Done " + title);
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                output.EndStep(this);
                disposed = true;
            }
        }
    }
}
