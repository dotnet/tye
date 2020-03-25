// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.IO;
using System.Threading;
using Microsoft.Tye.ConfigModel;
using Microsoft.Tye.Hosting;

namespace Microsoft.Tye
{
    static partial class Program
    {
        private static Command CreateRunCommand(string[] args)
        {
            var command = new Command("run", "run the application")
            {
                CommonArguments.Path_Required,
            };

            // TODO: We'll need to support a --build-args
            command.AddOption(new Option("--no-build")
            {
                Description = "Do not build project files before running.",
                Required = false
            });

            command.AddOption(new Option("--port")
            {
                Description = "The port to run control plane on.",
                Argument = new Argument<int>("port"),
                Required = false
            });

            command.AddOption(new Option("--logs")
            {
                Description = "Write structured application logs to the specified log providers. Supported providers are console, elastic (Elasticsearch), ai (ApplicationInsights), seq.",
                Argument = new Argument<string>("logs"),
                Required = false
            });

            command.AddOption(new Option("--dtrace")
            {
                Description = "Write distributed traces to the specified providers. Supported providers are zipkin.",
                Argument = new Argument<string>("logs"),
                Required = false
            });

            command.AddOption(new Option("--debug")
            {
                Argument = new Argument<string[]>("service"),
                Description = "Wait for debugger attach to specific service. Specify \"*\" to wait for all services.",
                Required = false
            });

            command.AddOption(new Option("--docker")
            {
                Description = "Run projects as docker containers.",
                Required = false
            });

            command.Handler = CommandHandler.Create<IConsole, FileInfo, string[]>(async (console, path, debug) =>
            {
                // Workaround for https://github.com/dotnet/command-line-api/issues/723#issuecomment-593062654
                if (path is null)
                {
                    throw new CommandException("No project or solution file was found.");
                }

                var output = new OutputContext(console, Verbosity.Quiet);
                var application = await ApplicationFactory.CreateAsync(output, path);
                var serviceCount = application.Services.Count;

                InitializeThreadPoolSettings(serviceCount);

                if (application.Services.Count == 0)
                {
                    throw new CommandException($"No services found in \"{application.Source.Name}\"");
                }

                using var host = new TyeHost(application.ToHostingApplication(), args, debug);
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
    }
}
