// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Tye.Hosting;

namespace Microsoft.Tye
{
    static partial class Program
    {
        private static Command CreateRunCommand()
        {
            var command = new Command("run", "run the application")
            {
                CommonArguments.Path_Required,

                new Option("--no-build")
                {
                    Description = "Do not build project files before running.",
                    Required = false
                },
                new Option("--port")
                {
                    Description = "The port to run control plane on.",
                    Argument = new Argument<int?>("port"),
                    Required = false
                },
                new Option("--logs")
                {
                    Description = "Write structured application logs to the specified log provider. Supported providers are 'console', 'elastic' (Elasticsearch), 'ai' (ApplicationInsights), 'seq'.",
                    Argument = new Argument<string>("logs"),
                    Required = false
                },
                new Option("--dtrace")
                {
                    Description = "Write distributed traces to the specified tracing provider. Supported providers are 'zipkin'.",
                    Argument = new Argument<string>("trace"),
                    Required = false,
                },
                new Option("--metrics")
                {
                    Description = "Write metrics to the specified metrics provider.",
                    Argument = new Argument<string>("metrics"),
                    Required = false
                },
                new Option("--debug")
                {
                    Argument = new Argument<string[]>("service")
                    {
                        Arity = ArgumentArity.ZeroOrMore,
                    },
                    Description = "Wait for debugger attach to specific service. Specify \"*\" to wait for all services.",
                    Required = false
                },
                new Option("--docker")
                {
                    Description = "Run projects as docker containers.",
                    Required = false
                },
                new Option("--dashboard")
                {
                    Description = "Launch dashboard on run.",
                    Required = false
                },
                new Option("--watch")
                {
                    Description = "Watches for code changes for all dotnet projects.",
                    Required = false
                },
                new Option("--no-start")
                {
                    Argument = new Argument<string[]>("service")
                    {
                        Arity = ArgumentArity.ZeroOrMore,
                    },
                    Description = "Skip automatic start for specific service(s). Specify \"*\" to skip start for all services.",
                    Required = false
                },
                StandardOptions.Framework,
                StandardOptions.Tags,
                StandardOptions.Verbosity,
            };

            command.Handler = CommandHandler.Create<RunCommandArguments>(async args =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (args.Path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(args.Console, args.Verbosity);

                output.WriteInfoLine("Loading Application Details...");

                var filter = ApplicationFactoryFilter.GetApplicationFactoryFilter(args.Tags);

                var application = await ApplicationFactory.CreateAsync(output, args.Path, args.Framework, filter);
                if (application.Services.Count == 0)
                {
                    throw new CommandException($"No services found in \"{application.Source.Name}\"");
                }

                var options = new HostOptions()
                {
                    Dashboard = args.Dashboard,
                    Docker = args.Docker,
                    NoBuild = args.NoBuild,
                    Port = args.Port,

                    // parsed later by the diagnostics code
                    DistributedTraceProvider = args.Dtrace,
                    LoggingProvider = args.Logs,
                    MetricsProvider = args.Metrics,
                    LogVerbosity = args.Verbosity,
                    Watch = args.Watch,
                };
                options.Debug.AddRange(args.Debug);
                options.NoStart.AddRange(args.NoStart);

                await application.ProcessExtensionsAsync(options, output, ExtensionContext.OperationKind.LocalRun);

                InitializeThreadPoolSettings(application.Services.Count);

                output.WriteInfoLine("Launching Tye Host...");
                output.WriteInfoLine(string.Empty);

                await using var host = new TyeHost(application.ToHostingApplication(), options);
                await host.RunAsync();
            });

            return command;
        }

        private static void InitializeThreadPoolSettings(int serviceCount)
        {
            ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);

            // We need to bump up the min threads to something reasonable so that the dashboard doesn't take forever
            // to serve requests. All console IO is blocking in .NET so capturing stdoutput and stderror results in blocking thread pool threads.
            // The thread pool handles bursts poorly and HTTP requests end up getting stuck behind spinning up docker containers and processes.

            // Bumping the min threads doesn't mean we'll have min threads to start, it just means don't add a threads very slowly up to
            // min threads
            ThreadPool.SetMinThreads(Math.Max(workerThreads, serviceCount * 4), completionPortThreads);

            // We use serviceCount * 4 because we currently launch multiple processes per service, this gives the dashboard some breathing room
        }

        // We have too many options to use the lambda form with each option as a parameter.
        // This is slightly cleaner anyway.
        private class RunCommandArguments
        {
            public IConsole Console { get; set; } = default!;

            public bool Dashboard { get; set; }

            public string[] Debug { get; set; } = Array.Empty<string>();

            public string[] NoStart { get; set; } = Array.Empty<string>();

            public string Dtrace { get; set; } = default!;

            public bool Docker { get; set; }

            public string Logs { get; set; } = default!;

            public string Metrics { get; set; } = default!;

            public bool NoBuild { get; set; }

            public FileInfo Path { get; set; } = default!;

            public int? Port { get; set; }

            public Verbosity Verbosity { get; set; }

            public bool Watch { get; set; }

            public string Framework { get; set; } = default!;

            public string[] Tags { get; set; } = Array.Empty<string>();
        }
    }
}
