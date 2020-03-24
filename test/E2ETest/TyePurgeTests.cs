// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Tye;
using Microsoft.Tye.ConfigModel;
using Microsoft.Tye.Hosting;
using Microsoft.Tye.Hosting.Model;
using Xunit;
using Xunit.Abstractions;

namespace E2ETest
{
    public class TyePurgeTests
    {
        private static readonly int MaxRetries = 5;

        private readonly ITestOutputHelper output;
        private readonly TestOutputLogEventSink sink;

        public TyePurgeTests(ITestOutputHelper output)
        {
            this.output = output;
            sink = new TestOutputLogEventSink(output);
        }

        [Fact]
        public async Task FrontendBackendPurgeTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "frontend-backend"));
            var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var tyeDir = new DirectoryInfo(Path.Combine(tempDirectory.DirectoryPath, ".tye"));
            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
            using var host = new TyeHost(application.ToHostingApplication(), Array.Empty<string>())
            {
                Sink = sink,
            };

            await host.StartAsync();
            try
            {
                await WaitUntilSpawned(host.Application);

                var pids = GetAllPids(host.Application);

                Assert.True(Directory.Exists(tyeDir.FullName));
                Assert.True(AllRunning(pids));

                await host.PurgeAsync();
                await Task.Delay(500);

                Assert.False(AnyRunning(pids));
            }
            finally
            {
                await host.StopAsync();
            }

            try
            {
                tempDirectory.Dispose();
            }
            catch (UnauthorizedAccessException) { }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task MultiProjectPurgeTest()
        {
            var projectDirectory = new DirectoryInfo(Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "samples", "multi-project"));
            var tempDirectory = TempDirectory.Create();
            DirectoryCopy.Copy(projectDirectory.FullName, tempDirectory.DirectoryPath);

            var projectFile = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "tye.yaml"));
            var tyeDir = new DirectoryInfo(Path.Combine(tempDirectory.DirectoryPath, ".tye"));
            var outputContext = new OutputContext(sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile);
            using var host = new TyeHost(application.ToHostingApplication(), Array.Empty<string>())
            {
                Sink = sink,
            };

            await host.StartAsync();
            try
            {
                await WaitUntilSpawned(host.Application);

                var pids = GetAllPids(host.Application);
                var containers = GetAllContainerIds(host.Application);

                Assert.True(Directory.Exists(tyeDir.FullName));
                Assert.True(AllRunning(pids));
                await DockerAssert.AssertAllContainersExistAsync(output, containers);

                await host.PurgeAsync();
                await Task.Delay(500);

                Assert.False(AnyRunning(pids));
                await DockerAssert.AssertNonContainersExistAsync(output, containers);
            }
            finally
            {
                await host.StopAsync();
            }

            try
            {
                tempDirectory.Dispose();
            }
            catch (UnauthorizedAccessException) { }
        }

        private int[] GetAllPids(Microsoft.Tye.Hosting.Model.Application application)
        {

            var replicas = application.Services.SelectMany(s => s.Value.Replicas);
            var ids = replicas.Where(r => r.Value is ProcessStatus).Select(r => ((ProcessStatus)r.Value).Pid ?? -1).ToArray();

            return ids;
        }

        private string[] GetAllContainerIds(Microsoft.Tye.Hosting.Model.Application application)
        {
            var replicas = application.Services.SelectMany(s => s.Value.Replicas);
            var ids = replicas.Where(r => r.Value is DockerStatus).Select(r => ((DockerStatus)r.Value).ContainerId!).ToArray();

            return ids;
        }

        private async Task WaitUntilSpawned(Microsoft.Tye.Hosting.Model.Application application)
        {
            int reties = 0;
            do
            {
                if (reties > MaxRetries)
                    throw new TimeoutException($"Could not reach response after {MaxRetries} reties");

                await Task.Delay(500);
                reties++;
            } while (application.Services.Any(a => a.Value.Description.Replicas > a.Value.Replicas.Count));
        }

        private bool AllRunning(int[] pids)
        {
            var allProcesses = new HashSet<int>(Process.GetProcesses().Select(p => p.Id));
            return pids.All(p => allProcesses.Contains(p));
        }

        private bool AnyRunning(int[] pids)
        {
            var allProcesses = Process.GetProcesses();
            var allProcessesId = new HashSet<int>(allProcesses.Select(p => p.Id));
            return pids.Any(p => allProcessesId.Contains(p));
        }
    }
}
