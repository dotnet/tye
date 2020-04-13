// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting;
using Microsoft.Tye.Hosting.Model;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Tye;
using Microsoft.Tye.ConfigModel;

namespace E2ETest
{
    public static class TestHelpers
    {
        private static readonly TimeSpan WaitForServicesTimeout = TimeSpan.FromSeconds(20);

        // https://github.com/dotnet/aspnetcore/blob/5a0526dfd991419d5bce0d8ea525b50df2e37b04/src/Testing/src/TestPathUtilities.cs
        // This can get into a bad pattern for having crazy paths in places. Eventually, especially if we use helix,
        // we may want to avoid relying on sln position.
        public static string GetSolutionRootDirectory(string solution)
        {
            var applicationBasePath = AppContext.BaseDirectory;
            var directoryInfo = new DirectoryInfo(applicationBasePath);

            do
            {
                var projectFileInfo = new FileInfo(Path.Combine(directoryInfo.FullName, $"{solution}.sln"));
                if (projectFileInfo.Exists)
                {
                    return projectFileInfo.DirectoryName;
                }

                directoryInfo = directoryInfo.Parent;
            }
            while (directoryInfo.Parent != null);

            throw new Exception($"Solution file {solution}.sln could not be found in {applicationBasePath} or its parent directories.");
        }

        public static DirectoryInfo GetTestAssetsDirectory()
        {
            return new DirectoryInfo(Path.Combine(
                TestHelpers.GetSolutionRootDirectory("tye"),
                "test",
                "E2ETest",
                "testassets"));
        }

        public static DirectoryInfo GetTestProjectDirectory(string projectName)
        {
            var directory = new DirectoryInfo(Path.Combine(
                TestHelpers.GetSolutionRootDirectory("tye"),
                "test",
                "E2ETest",
                "testassets",
                "projects",
                projectName));
            Assert.True(directory.Exists, $"Project {projectName} not found.");
            return directory;
        }

        public static DirectoryInfo GetSampleProjectDirectory(string projectName)
        {
            var directory = new DirectoryInfo(Path.Combine(
                TestHelpers.GetSolutionRootDirectory("tye"),
                "samples",
                projectName));
            Assert.True(directory.Exists, $"Project {projectName} not found.");
            return directory;
        }

        internal static TempDirectory CopyTestProjectDirectory(string projectName)
        {
            var temp = TempDirectory.Create(preferUserDirectoryOnMacOS: true);
            DirectoryCopy.Copy(GetTestProjectDirectory(projectName).FullName, temp.DirectoryPath);

            // We need to hijack any P2P references to Tye libraries.
            // Test projects must use $(TyeLibrariesPath) to find their references.
            var libraryPath = Path.Combine(TestHelpers.GetSolutionRootDirectory("tye"), "src");
            if (!libraryPath.EndsWith(Path.DirectorySeparatorChar))
            {
                libraryPath += Path.DirectorySeparatorChar;
            }

            File.WriteAllText(
                Path.Combine(temp.DirectoryPath, "Directory.Build.props"),
                $@"
<Project>
  <PropertyGroup>
    <TyeLibrariesPath>{libraryPath}</TyeLibrariesPath>
  </PropertyGroup>
</Project>");

            return temp;
        }

        public static async Task StartHostAndWaitForReplicasToStart(TyeHost host)
        {
            var startedTask = new TaskCompletionSource<bool>();
            var alreadyStarted = 0;
            var totalReplicas = host.Application.Services.Sum(s => s.Value.Description.Replicas);

            void OnReplicaChange(ReplicaEvent ev)
            {
                if (ev.State == ReplicaState.Started)
                {
                    Interlocked.Increment(ref alreadyStarted);
                }
                else if (ev.State == ReplicaState.Stopped)
                {
                    Interlocked.Decrement(ref alreadyStarted);
                }

                if (alreadyStarted == totalReplicas)
                {
                    startedTask.TrySetResult(true);
                }
            }

            var servicesStateObserver = host.Application.Services.Select(srv => srv.Value.ReplicaEvents.Subscribe(OnReplicaChange)).ToList();
            await host.StartAsync();

            using var cancellation = new CancellationTokenSource(WaitForServicesTimeout);
            try
            {
                await using (cancellation.Token.Register(() => startedTask.TrySetCanceled()))
                {
                    await startedTask.Task;
                }
            }
            finally
            {
                foreach (var observer in servicesStateObserver)
                {
                    observer.Dispose();
                }
            }
        }

        public static async Task PurgeHostAndWaitForGivenReplicasToStop(TyeHost host, string[] replicas)
        {
            static async Task Purge(TyeHost host)
            {
                var logger = host.DashboardWebApplication!.Logger;
                var replicaRegistry = new ReplicaRegistry(host.Application.ContextDirectory, logger);
                var processRunner = new ProcessRunner(logger, replicaRegistry, new ProcessRunnerOptions());
                var dockerRunner = new DockerRunner(logger, replicaRegistry);

                await processRunner.StartAsync(new Application(new FileInfo(host.Application.Source), new Dictionary<string, Service>()));
                await dockerRunner.StartAsync(new Application(new FileInfo(host.Application.Source), new Dictionary<string, Service>()));
            }

            var stoppedTask = new TaskCompletionSource<bool>();
            var remaining = replicas.Length;

            void OnReplicaChange(ReplicaEvent ev)
            {
                if (replicas.Contains(ev.Replica.Name) && ev.State == ReplicaState.Stopped)
                {
                    Interlocked.Decrement(ref remaining);
                }

                if (remaining == 0)
                {
                    stoppedTask.TrySetResult(true);
                }
            }

            var servicesStateObserver = host.Application.Services.Select(srv => srv.Value.ReplicaEvents.Subscribe(OnReplicaChange)).ToList();

            // We purge existing replicas by restarting the host which will initiate the purging process
            await Purge(host);

            using var cancellation = new CancellationTokenSource(WaitForServicesTimeout);
            try
            {
                await using (cancellation.Token.Register(() => stoppedTask.TrySetCanceled()))
                {
                    await stoppedTask.Task;
                }
            }
            finally
            {
                foreach (var observer in servicesStateObserver)
                {
                    observer.Dispose();
                }
            }
        }

        public static void CompareConfigApplications(ConfigApplication expected, ConfigApplication actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Registry, actual.Registry);
            Assert.Equal(expected.Network, actual.Network);

            foreach (var ingress in actual.Ingress)
            {
                var otherIngress = expected
                    .Ingress
                    .Where(o => o.Name == ingress.Name)
                    .Single();
                Assert.NotNull(otherIngress);
                Assert.Equal(otherIngress.Replicas, ingress.Replicas);

                foreach (var rule in ingress.Rules)
                {
                    var otherRule = otherIngress
                        .Rules
                        .Where(o => o.Path == rule.Path && o.Host == rule.Host && o.Service == rule.Service)
                        .Single();
                    Assert.NotNull(otherRule);
                }

                foreach (var binding in ingress.Bindings)
                {
                    var otherBinding = otherIngress
                        .Bindings
                        .Where(o => o.Name == binding.Name && o.Port == binding.Port && o.Protocol == binding.Protocol)
                        .Single();

                    Assert.NotNull(otherBinding);
                }
            }

            foreach (var service in actual.Services)
            {
                var otherService = expected
                    .Services
                    .Where(o => o.Name == service.Name)
                    .Single();
                Assert.NotNull(otherService);
                Assert.Equal(otherService.Args, service.Args);
                Assert.Equal(otherService.Build, service.Build);
                Assert.Equal(otherService.Executable, service.Executable);
                Assert.Equal(otherService.External, service.External);
                Assert.Equal(otherService.Image, service.Image);
                Assert.Equal(otherService.Project, service.Project);
                Assert.Equal(otherService.Replicas, service.Replicas);
                Assert.Equal(otherService.WorkingDirectory, service.WorkingDirectory);

                foreach (var binding in service.Bindings)
                {
                    var otherBinding = otherService.Bindings
                                    .Where(o => o.Name == binding.Name
                                        && o.Port == binding.Port
                                        && o.Protocol == binding.Protocol
                                        && o.ConnectionString == binding.ConnectionString
                                        && o.ContainerPort == binding.ContainerPort
                                        && o.Host == binding.Host)
                                    .Single();

                    Assert.NotNull(otherBinding);
                }

                foreach (var binding in service.Bindings)
                {
                    var otherBinding = otherService.Bindings
                                    .Where(o => o.Name == binding.Name
                                        && o.Port == binding.Port
                                        && o.Protocol == binding.Protocol
                                        && o.ConnectionString == binding.ConnectionString
                                        && o.ContainerPort == binding.ContainerPort
                                        && o.Host == binding.Host)
                                    .Single();

                    Assert.NotNull(otherBinding);
                }

                foreach (var config in service.Configuration)
                {
                    var otherConfig = otherService.Configuration
                                    .Where(o => o.Name == config.Name
                                        && o.Value == config.Value)
                                    .Single();

                    Assert.NotNull(otherConfig);
                }

                foreach (var volume in service.Volumes)
                {
                    var otherVolume = otherService.Volumes
                                   .Where(o => o.Name == volume.Name
                                       && o.Target == volume.Target
                                       && o.Source == volume.Source)
                                   .Single();
                    Assert.NotNull(otherVolume);
                }
            }
        }
    }
}
