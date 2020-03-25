// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;
using Semver;

namespace Microsoft.Tye
{
    public static class ProjectReader
    {
        private static object @lock = new object();
        private static bool registered;

        public static IEnumerable<FileInfo> EnumerateProjects(FileInfo solutionFile)
        {
            EnsureMSBuildRegistered(null, solutionFile);
            return EnumerateProjectsCore(solutionFile);
        }

        // Do not load MSBuild types before using EnsureMSBuildRegistered.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<FileInfo> EnumerateProjectsCore(FileInfo solutionFile)
        {
            var solution = SolutionFile.Parse(solutionFile.FullName);
            foreach (var project in solution.ProjectsInOrder)
            {
                if (project.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat)
                {
                    continue;
                }

                var extension = Path.GetExtension(project.AbsolutePath).ToLower();
                switch (extension)
                {
                    case ".csproj":
                    case ".fsproj":
                        break;
                    default:
                        continue;
                }

                yield return new FileInfo(project.AbsolutePath.Replace('\\', '/'));
            }
        }

        public static Task ReadProjectDetailsAsync(OutputContext output, ProjectServiceBuilder project)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            EnsureMSBuildRegistered(output, project.ProjectFile);

            EvaluateProject(output, project);

            if (!SemVersion.TryParse(project.Version, out var version))
            {
                output.WriteInfoLine($"No version or invalid version '{project.Version}' found, using default.");
                version = new SemVersion(0, 1, 0);
                project.Version = version.ToString();
            }

            return Task.CompletedTask;
        }

        private static void EnsureMSBuildRegistered(OutputContext? output, FileInfo projectFile)
        {
            if (!registered)
            {
                lock (@lock)
                {
                    output?.WriteDebugLine("Locating .NET SDK...");

                    // It says VisualStudio - but we'll just use .NET SDK
                    var instances = MSBuildLocator.QueryVisualStudioInstances(new VisualStudioInstanceQueryOptions()
                    {
                        DiscoveryTypes = DiscoveryType.DotNetSdk,

                        // Using the project as the working directory. We're making the assumption that
                        // all of the projects want to use the same SDK version. This library is going
                        // load a single version of the SDK's assemblies into our process, so we can't
                        // use supprt SDKs at once without getting really tricky.
                        //
                        // The .NET SDK-based discovery uses `dotnet --info` and returns the SDK
                        // in use for the directory.
                        //
                        // https://github.com/microsoft/MSBuildLocator/blob/master/src/MSBuildLocator/MSBuildLocator.cs#L320
                        WorkingDirectory = projectFile.DirectoryName,
                    });

                    var instance = instances.SingleOrDefault();
                    if (instance == null)
                    {
                        throw new CommandException("Failed to find dotnet. Make sure the .NET SDK is installed and on the PATH.");
                    }

                    output?.WriteDebugLine("Found .NET SDK at: " + instance.MSBuildPath);

                    try
                    {
                        MSBuildLocator.RegisterInstance(instance);
                        output?.WriteDebug("Registered .NET SDK.");
                    }
                    finally
                    {
                        registered = true;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LogIt(OutputContext output)
        {
            output.WriteDebugLine("Loaded: " + typeof(ProjectInstance).Assembly.FullName);
            output.WriteDebugLine("Loaded From: " + typeof(ProjectInstance).Assembly.Location);
        }

        // Do not load MSBuild types before using EnsureMSBuildRegistered.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void EvaluateProject(OutputContext output, ProjectServiceBuilder project)
        {
            var sw = Stopwatch.StartNew();

            // We need to isolate projects from each other for testing. MSBuild does not support
            // loading the same project twice in the same collection.
            var projectCollection = new ProjectCollection();

            ProjectInstance projectInstance;

            try
            {
                output.WriteDebugLine($"Loading project '{project.ProjectFile.FullName}'.");
                var msbuildProject = Microsoft.Build.Evaluation.Project.FromFile(project.ProjectFile.FullName, new ProjectOptions()
                {
                    ProjectCollection = projectCollection,
                });
                projectInstance = msbuildProject.CreateProjectInstance();
                output.WriteDebugLine($"Loaded project '{project.ProjectFile.FullName}'.");
            }
            catch (Exception ex)
            {
                throw new CommandException($"Failed to load project: '{project.ProjectFile.FullName}'.", ex);
            }

            // Currently we only log at debug level.
            var logger = new ConsoleLogger(
                verbosity: LoggerVerbosity.Normal,
                write: message => output.WriteDebug(message),
                colorSet: null,
                colorReset: null);

            try
            {
                AssemblyLoadContext.Default.Resolving += ResolveAssembly;
                var result = projectInstance.Build(
                    targets: new[] { "Restore", "ResolveReferences", "ResolvePackageDependenciesDesignTime", "PrepareResources", "GetAssemblyAttributes", },
                    loggers: new[] { logger, });

                // If the build fails, we're not really blocked from doing our work.
                // For now we just log the output to debug. There are errors that occur during
                // running these targets we don't really care as long as we get the data.
            }
            finally
            {
                AssemblyLoadContext.Default.Resolving -= ResolveAssembly;
            }

            // Reading both InformationalVersion and Version is more resilant in the face of build failures.
            var version = projectInstance.GetProperty("InformationalVersion")?.EvaluatedValue ?? projectInstance.GetProperty("Version").EvaluatedValue;
            project.Version = version;
            output.WriteDebugLine($"Found application version: {version}");

            var targetFrameworks = projectInstance.GetPropertyValue("TargetFrameworks");
            project.TargetFrameworks = targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            project.RunCommand = projectInstance.GetPropertyValue("RunCommand");
            project.RunArguments = projectInstance.GetPropertyValue("RunArguments");
            project.TargetPath = projectInstance.GetPropertyValue("TargetPath");
            project.PublishDir = projectInstance.GetPropertyValue("PublishDir");
            project.AssemblyName = projectInstance.GetPropertyValue("AssemblyName");
            project.IntermediateOutputPath = projectInstance.GetPropertyValue("IntermediateOutputPath");

            output.WriteDebugLine($"RunCommand={project.RunCommand}");
            output.WriteDebugLine($"RunArguments={project.RunArguments}");
            output.WriteDebugLine($"TargetPath={project.TargetPath}");
            output.WriteDebugLine($"PublishDir={project.PublishDir}");
            output.WriteDebugLine($"AssemblyName={project.AssemblyName}");
            output.WriteDebugLine($"IntermediateOutputPath={project.IntermediateOutputPath}");

            // Normalize directories to their absolute paths
            project.IntermediateOutputPath = Path.Combine(project.ProjectFile.DirectoryName, project.IntermediateOutputPath);
            project.TargetPath = Path.Combine(project.ProjectFile.DirectoryName, project.TargetPath);
            project.PublishDir = Path.Combine(project.ProjectFile.DirectoryName, project.PublishDir);

            var targetFramework = projectInstance.GetPropertyValue("TargetFramework");
            project.TargetFramework = targetFramework;
            output.WriteDebugLine($"Found target framework: {targetFramework}");

            var sharedFrameworks = projectInstance.GetItems("FrameworkReference").Select(i => i.EvaluatedInclude).ToList();
            project.Frameworks.AddRange(sharedFrameworks.Select(s => new Framework(s)));
            output.WriteDebugLine($"Found shared frameworks: {string.Join(", ", sharedFrameworks)}");

            output.WriteDebugLine($"Evaluation Took: {sw.Elapsed.TotalMilliseconds}ms");

            // The Microsoft.Build.Locator doesn't handle the loading of other assemblies
            // that are shipped with MSBuild (ex NuGet).
            //
            // This means that the set of assemblies that need special handling depends on the targets
            // that we run :(
            //
            // This is workaround for this limitation based on the targets we need to run
            // to resolve references and versions.
            //
            // See: https://github.com/microsoft/MSBuildLocator/issues/86
            Assembly? ResolveAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
            {
                if (assemblyName.Name is object && assemblyName.Name.StartsWith("NuGet."))
                {
                    var msbuildDirectory = Environment.GetEnvironmentVariable("MSBuildExtensionsPath")!;
                    var assemblyFilePath = Path.Combine(msbuildDirectory, assemblyName.Name + ".dll");
                    if (File.Exists(assemblyFilePath))
                    {
                        return context.LoadFromAssemblyPath(assemblyFilePath);
                    }
                }

                return default;
            }
        }
    }
}
