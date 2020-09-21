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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Locator;
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
                        // use support SDKs at once without getting really tricky.
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

        public static async Task ReadProjectDetailsAsync(OutputContext output, DotnetProjectServiceBuilder project)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (!Directory.Exists(project.ProjectFile.DirectoryName))
            {
                throw new CommandException($"Failed to locate directory: '{project.ProjectFile.DirectoryName}'.");
            }

            await EvaluateProjectAsync(output, project);

            if (!SemVersion.TryParse(project.Version, out var version))
            {
                output.WriteInfoLine($"No version or invalid version '{project.Version}' found, using default.");
                version = new SemVersion(0, 1, 0);
                project.Version = version.ToString();
            }
        }

        // Do not load MSBuild types before using EnsureMSBuildRegistered.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task EvaluateProjectAsync(OutputContext output, DotnetProjectServiceBuilder project)
        {
            var sw = Stopwatch.StartNew();

            var metadata = new Dictionary<string, string>();

            var msbuildArgs = "msbuild " +
                "/t:Restore " +
                "/t:MicrosoftTye_GetProjectMetadata " +
                $"/p:CustomAfterMicrosoftCommonTargets=\"{Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "ProjectEvaluation.targets")}\" ";
            if (project.BuildProperties.Any())
            {
                msbuildArgs += $"{project.BuildProperties.Select(kvp => $"/p:{kvp.Key}={kvp.Value}").Aggregate((a, b) => a + " " + b)} ";
            }
            msbuildArgs += $"\"{project.ProjectFile.FullName}\" /nologo";

            output.WriteDebugLine($"Running msbuild command: dotnet {msbuildArgs}");

            var buildResult = await ProcessUtil.RunAsync("dotnet", msbuildArgs, throwOnError: false);

            // If the build fails, we're not really blocked from doing our work.
            // For now we just log the output to debug. There are errors that occur during
            // running these targets we don't really care as long as we get the data.
            if (buildResult.ExitCode != 0)
            {
                output.WriteDebugLine($"Evaluating project failed with exit code {buildResult.ExitCode}:" +
                    $"{Environment.NewLine}{buildResult.StandardError}");
            }

            var metadataFileMessage = buildResult
                .StandardOutput
                .Split(Environment.NewLine)
                .Select(s => s.Trim())
                .SingleOrDefault(s => s.StartsWith("Microsoft.Tye metadata file:"));

            // If project evaluation is successful this should not happen, therefore an exception will be thrown.
            if (string.IsNullOrEmpty(metadataFileMessage))
            {
                throw new CommandException($"Evaluated project metadata file could not be found:" +
                    $"{Environment.NewLine}{buildResult.StandardError}");
            }

            var metadataFilePath = metadataFileMessage.Split(':', 2)[1].TrimStart();
            var metadataKVPs = File.ReadLines(metadataFilePath).Select(l => l.Split(new[] { ':' }, 2));

            foreach (var metadataKVP in metadataKVPs)
            {
                if (!string.IsNullOrEmpty(metadataKVP[1]))
                {
                    metadata.Add(metadataKVP[0], metadataKVP[1].Trim());
                }
            }

            // Reading a few different version properties to be more resilient.
            var version = GetMetadataValueOrNull("AssemblyInformationalVersion") ??
                 GetMetadataValueOrNull("InformationalVersion") ??
                 GetMetadataValueOrEmpty("Version");
            project.Version = version;
            output.WriteDebugLine($"Found application version: {version}");

            project.TargetFrameworks = GetMetadataValueOrNull("TargetFrameworks")?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            // Figure out if functions app.
            // If so, run app with function host.
            project.RunCommand = GetMetadataValueOrEmpty("RunCommand");
            project.RunArguments = GetMetadataValueOrEmpty("RunArguments");
            project.TargetPath = GetMetadataValueOrEmpty("TargetPath");
            project.PublishDir = GetMetadataValueOrEmpty("PublishDir");
            project.AssemblyName = GetMetadataValueOrEmpty("AssemblyName");
            project.IntermediateOutputPath = GetMetadataValueOrEmpty("IntermediateOutputPath");

            output.WriteDebugLine($"RunCommand={project.RunCommand}");
            output.WriteDebugLine($"RunArguments={project.RunArguments}");
            output.WriteDebugLine($"TargetPath={project.TargetPath}");
            output.WriteDebugLine($"PublishDir={project.PublishDir}");
            output.WriteDebugLine($"AssemblyName={project.AssemblyName}");
            output.WriteDebugLine($"IntermediateOutputPath={project.IntermediateOutputPath}");

            // Normalize directories to their absolute paths
            project.IntermediateOutputPath = Path.Combine(project.ProjectFile.DirectoryName!, NormalizePath(project.IntermediateOutputPath));
            project.TargetPath = Path.Combine(project.ProjectFile.DirectoryName!, NormalizePath(project.TargetPath));
            project.PublishDir = Path.Combine(project.ProjectFile.DirectoryName!, NormalizePath(project.PublishDir));

            var targetFramework = GetMetadataValueOrEmpty("TargetFramework");
            project.TargetFramework = targetFramework;
            output.WriteDebugLine($"Found target framework: {targetFramework}");

            // TODO: Parse the name and version manually out of the TargetFramework field if it's non-null
            project.TargetFrameworkName = GetMetadataValueOrEmpty("_ShortFrameworkIdentifier");
            project.TargetFrameworkVersion = GetMetadataValueOrNull("_ShortFrameworkVersion") ?? GetMetadataValueOrEmpty("_TargetFrameworkVersionWithoutV");

            var sharedFrameworks = GetMetadataValueOrNull("FrameworkReference")?.Split(';') ?? Enumerable.Empty<string>();
            project.Frameworks.AddRange(sharedFrameworks.Select(s => new Framework(s)));
            output.WriteDebugLine($"Found shared frameworks: {string.Join(", ", sharedFrameworks)}");

            // determine container base image
            if (project.ContainerInfo != null)
            {
                project.ContainerInfo.BaseImageName = GetMetadataValueOrEmpty("ContainerBaseImage");
                project.ContainerInfo.BaseImageTag = GetMetadataValueOrEmpty("ContainerBaseTag");
            }

            project.IsAspNet = project.Frameworks.Any(f => f.Name == "Microsoft.AspNetCore.App") ||
                               GetMetadataValueOrEmpty("MicrosoftNETPlatformLibrary") == "Microsoft.AspNetCore.App" ||
                               MetadataIsTrue("_AspNetCoreAppSharedFxIsEnabled") ||
                               MetadataIsTrue("UsingMicrosoftNETSdkWeb");

            output.WriteDebugLine($"IsAspNet={project.IsAspNet}");

            output.WriteDebugLine($"Evaluation Took: {sw.Elapsed.TotalMilliseconds}ms");

            string? GetMetadataValueOrNull(string key) => metadata!.TryGetValue(key, out var value) ? value : null;
            string GetMetadataValueOrEmpty(string key) => metadata!.TryGetValue(key, out var value) ? value : string.Empty;
            bool MetadataIsTrue(string key) => metadata!.TryGetValue(key, out var value) && bool.Parse(value);
        }

        private static string NormalizePath(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return path.Replace('/', '\\');
            }
            return path.Replace('\\', '/');
        }
    }
}
