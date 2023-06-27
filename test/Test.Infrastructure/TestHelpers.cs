// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting;
using Microsoft.Tye.Hosting.Model;
using Xunit;
using Microsoft.Tye;
using System.Diagnostics;

namespace Test.Infrastructure
{
    public static class TestHelpers
    {
        private static readonly TimeSpan WaitForServicesTimeout = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(1);

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

        public static TempDirectory CopyTestProjectDirectory(string projectName)
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

        public static async Task<bool> DoOperationAndWaitForReplicasToChangeState(TyeHost host, ReplicaState desiredState, int n, HashSet<string> toChange, HashSet<string> rest, Func<ReplicaEvent, string> entitySelector, TimeSpan waitUntilSuccess, Func<TyeHost, Task> operation)
        {
            if (toChange != null && rest != null && rest.Overlaps(toChange))
            {
                throw new ArgumentException($"{nameof(toChange)} and {nameof(rest)} can't overlap");
            }

            var changedTask = new TaskCompletionSource<bool>();
            var remaining = n;

            void OnReplicaChange(ReplicaEvent ev)
            {
                if (rest != null && rest.Contains(entitySelector(ev)))
                {
                    changedTask!.TrySetResult(false);
                }
                else if ((toChange == null || toChange.Contains(entitySelector(ev))) && ev.State == desiredState)
                {
                    Interlocked.Decrement(ref remaining);
                }

                if (remaining == 0)
                {
                    Task.Delay(waitUntilSuccess)
                        .ContinueWith(_ =>
                        {
                            if (!changedTask!.Task.IsCompleted)
                            {
                                changedTask!.TrySetResult(remaining == 0);
                            }
                        });
                }
            }

            var servicesStateObserver = host.Application.Services.Select(srv => srv.Value.ReplicaEvents.Subscribe(OnReplicaChange)).ToList();

            await operation(host);

            using var cancellation = new CancellationTokenSource(WaitForServicesTimeout);
            try
            {
                await using (cancellation.Token.Register(() => changedTask.TrySetCanceled()))
                {
                    return await changedTask.Task;
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

        public static async Task<bool> DoOperationAndWaitForReplicasToRestart(TyeHost host, HashSet<string> toRestart, HashSet<string> rest, Func<ReplicaEvent, string> entitySelector, TimeSpan waitUntilSuccess, Func<TyeHost, Task> operation)
        {
            if (rest != null && rest.Overlaps(toRestart))
            {
                throw new ArgumentException($"{nameof(toRestart)} and {nameof(rest)} can't overlap");
            }

            var restartedTask = new TaskCompletionSource<bool>();
            var remaining = toRestart.Count;
            var alreadyStarted = 0;

            void OnReplicaChange(ReplicaEvent ev)
            {
                if (ev.State == ReplicaState.Started)
                {
                    Interlocked.Increment(ref alreadyStarted);
                }
                else if (ev.State == ReplicaState.Stopped)
                {
                    if (toRestart.Contains(entitySelector(ev)))
                    {
                        Interlocked.Decrement(ref remaining);
                    }
                    else if (rest != null && rest.Contains(entitySelector(ev)))
                    {
                        restartedTask!.SetResult(false);
                    }
                }

                if (remaining == 0 && alreadyStarted == toRestart.Count)
                {
                    Task.Delay(waitUntilSuccess)
                        .ContinueWith(_ =>
                        {
                            if (!restartedTask!.Task.IsCompleted)
                            {
                                restartedTask!.SetResult(remaining == 0 && alreadyStarted == toRestart.Count);
                            }
                        });
                }
            }

            var servicesStateObserver = host.Application.Services.Select(srv => srv.Value.ReplicaEvents.Subscribe(OnReplicaChange)).ToList();

            await operation(host);

            using var cancellation = new CancellationTokenSource(WaitForServicesTimeout);
            try
            {
                await using (cancellation.Token.Register(() => restartedTask.TrySetCanceled()))
                {
                    return await restartedTask.Task;
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

        public static Task<bool> DoOperationAndWaitForReplicasToChangeState(TyeHost host, ReplicaState desiredState, int n, HashSet<string> toChange, HashSet<string> rest, TimeSpan waitUntilSuccess, Func<TyeHost, Task> operation)
            => DoOperationAndWaitForReplicasToChangeState(host, desiredState, n, toChange, rest, ev => ev.Replica.Name, waitUntilSuccess, operation);

        public static Task<bool> DoOperationAndWaitForReplicasToRestart(TyeHost host, HashSet<string> toRestart, HashSet<string> rest, TimeSpan waitUntilSuccess, Func<TyeHost, Task> operation)
            => DoOperationAndWaitForReplicasToRestart(host, toRestart, rest, ev => ev.Replica.Name, waitUntilSuccess, operation);

        public static async Task StartHostAndWaitForReplicasToStart(TyeHost host, string[] services = null, ReplicaState desiredState = ReplicaState.Started)
        {
            if (services == null)
            {
                await DoOperationAndWaitForReplicasToChangeState(host, desiredState, host.Application.Services.Sum(s => s.Value.Description.Replicas), null, null, TimeSpan.Zero, h => h.StartAsync());
            }
            else
            {
                if (services.Any(s => !host.Application.Services.ContainsKey(s)))
                {
                    throw new ArgumentException($"not all services given in {nameof(services)} exist");
                }

                await DoOperationAndWaitForReplicasToChangeState(host, desiredState, host.Application.Services.Where(s => services.Contains(s.Value.Description.Name)).Sum(s => s.Value.Description.Replicas), services.ToHashSet(), null, ev => ev.Replica.Service.Description.Name, TimeSpan.Zero, h => h.StartAsync());
            }
        }

        public static async Task PurgeHostAndWaitForGivenReplicasToStop(TyeHost host, string[] replicas)
        {
            static async Task Purge(TyeHost host)
            {
                var logger = host.Logger;
                var replicaRegistry = new ReplicaRegistry(host.Application.ContextDirectory, logger);
                var processRunner = new ProcessRunner(logger, replicaRegistry, new ProcessRunnerOptions());
                var dockerRunner = new DockerRunner(logger, replicaRegistry, new DockerRunnerOptions());

                await processRunner.StartAsync(new Application(host.Application.Name, new FileInfo(host.Application.Source), null, new Dictionary<string, Service>(), ContainerEngine.Default));
                await dockerRunner.StartAsync(new Application(host.Application.Name, new FileInfo(host.Application.Source), null, new Dictionary<string, Service>(), ContainerEngine.Default));
            }

            await DoOperationAndWaitForReplicasToChangeState(host, ReplicaState.Stopped, replicas.Length, replicas.ToHashSet(), new HashSet<string>(), TimeSpan.Zero, Purge);
        }
    }
}
