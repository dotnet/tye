// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Tye;
using Microsoft.Tye.Hosting;
using Microsoft.Tye.Hosting.Model;
using Test.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using static Test.Infrastructure.TestHelpers;

namespace E2ETest
{
    public class TyePurgeTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TestOutputLogEventSink _sink;

        public TyePurgeTests(ITestOutputHelper output)
        {
            _output = output;
            _sink = new TestOutputLogEventSink(output);
        }

        [Fact]
        public async Task FrontendBackendPurgeTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("frontend-backend");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var tyeDir = new DirectoryInfo(Path.Combine(projectDirectory.DirectoryPath, ".tye"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);
            var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            try
            {
                await TestHelpers.StartHostAndWaitForReplicasToStart(host);

                var pids = GetAllAppPids(host.Application);

                Assert.True(Directory.Exists(tyeDir.FullName));
                Assert.Subset(new HashSet<int>(GetAllPids()), new HashSet<int>(pids));

                await TestHelpers.PurgeHostAndWaitForGivenReplicasToStop(host,
                    GetAllReplicasNames(host.Application));

                var runningPids = new HashSet<int>(GetAllPids());
                Assert.True(pids.All(pid => !runningPids.Contains(pid)));

            }
            finally
            {
                await host.DisposeAsync();
                Assert.False(Directory.Exists(tyeDir.FullName));
            }
        }

        [ConditionalFact]
        [SkipIfDockerNotRunning]
        public async Task MultiProjectPurgeTest()
        {
            using var projectDirectory = CopyTestProjectDirectory("multi-project");

            var projectFile = new FileInfo(Path.Combine(projectDirectory.DirectoryPath, "tye.yaml"));
            var tyeDir = new DirectoryInfo(Path.Combine(projectDirectory.DirectoryPath, ".tye"));
            var outputContext = new OutputContext(_sink, Verbosity.Debug);
            var application = await ApplicationFactory.CreateAsync(outputContext, projectFile, null);
            var host = new TyeHost(application.ToHostingApplication(), new HostOptions())
            {
                Sink = _sink,
            };

            try
            {
                await TestHelpers.StartHostAndWaitForReplicasToStart(host);

                var pids = GetAllAppPids(host.Application);
                var containers = GetAllContainerIds(host.Application);

                Assert.True(Directory.Exists(tyeDir.FullName));
                Assert.Subset(new HashSet<int>(GetAllPids()), new HashSet<int>(pids));
                Assert.Subset(new HashSet<string>(await DockerAssert.GetRunningContainersIdsAsync(_output)),
                    new HashSet<string>(containers));

                await TestHelpers.PurgeHostAndWaitForGivenReplicasToStop(host,
                    GetAllReplicasNames(host.Application));

                var runningPids = new HashSet<int>(GetAllPids());
                Assert.True(pids.All(pid => !runningPids.Contains(pid)));
                var runningContainers =
                    new HashSet<string>(await DockerAssert.GetRunningContainersIdsAsync(_output));
                Assert.True(containers.All(c => !runningContainers.Contains(c)));

            }
            finally
            {
                await host.DisposeAsync();
                Assert.False(Directory.Exists(tyeDir.FullName));
            }
        }

        private string[] GetAllReplicasNames(Microsoft.Tye.Hosting.Model.Application application)
        {
            var replicas = application.Services.SelectMany(s => s.Value.Replicas);
            return replicas.Select(r => r.Value.Name).ToArray();
        }

        private int[] GetAllAppPids(Microsoft.Tye.Hosting.Model.Application application)
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

        private int[] GetAllPids()
        {
            return Process.GetProcesses().Select(p => p.Id).ToArray();
        }
    }
}
