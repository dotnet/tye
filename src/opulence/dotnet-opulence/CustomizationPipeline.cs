using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Construction;

namespace Opulence
{
    internal class CustomizationPipeline : Pipeline
    {
        private readonly Dictionary<Type, List<MulticastDelegate>> callbacks = new Dictionary<Type, List<MulticastDelegate>>();

        private readonly OutputContext output;
        private readonly string rootDirectory;
        private readonly string name;
        private readonly SolutionFile? solution;
        private readonly FileInfo? projectFile;

        public CustomizationPipeline(OutputContext output, string rootDirectory, string name, SolutionFile? solution, FileInfo? projectFile)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (rootDirectory is null)
            {
                throw new ArgumentNullException(nameof(rootDirectory));
            }

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            this.output = output;
            this.rootDirectory = rootDirectory;
            this.name = name;
            this.solution = solution;
            this.projectFile = projectFile;
        }

        public override async Task<object?> ExecuteAsync()
        {
            if (callbacks.Count == 0)
            {
                return Task.FromResult<object?>(null);
            }

            if (callbacks.Count > 1)
            {
                throw new InvalidOperationException("More than one application type is not supported.");
            }

            var kvp = callbacks.Single();

            var type = kvp.Key;
            var delegates = kvp.Value;

            output.WriteDebugLine($"Creating instance of application type '{type}'.");
            var application = Activator.CreateInstance(type);
            output.WriteDebugLine($"Done creating instance of application type '{type}'.");

            var wrapper = new ApplicationWrapper(application!, rootDirectory);

            wrapper.Globals.Name ??= name;

            foreach (var service in wrapper.Services)
            {
                output.WriteDebugLine($"Found service '{service.FriendlyName} {{ Name: {service.Service.Name} }}'.");

                string? projectRelativeFilePath = null;
                string? projectFilePath = null;
                if (solution != null)
                {
                    var project = FindProjectInSolution(solution,  service.FriendlyName);
                    if (project == null)
                    {
                        output.WriteDebugLine($"Could not find project for service '{service.FriendlyName}'.");
                        continue;
                    }

                    output.WriteDebugLine($"Found project '{project.RelativePath}' for service '{service.FriendlyName}'.");
                    projectRelativeFilePath = project.RelativePath.Replace('\\', Path.DirectorySeparatorChar);
                    projectFilePath = project.AbsolutePath.Replace('\\', Path.DirectorySeparatorChar);
                }
                else if (projectFile != null)
                {
                    var normalized = Names.NormalizeToFriendly(Path.GetFileNameWithoutExtension(projectFile.Name));
                    if (!string.Equals(normalized, service.FriendlyName))
                    {
                        output.WriteDebugLine($"Skipping service '{service.FriendlyName}'.");
                        continue;
                    }

                    projectRelativeFilePath = projectFile.FullName;
                    projectFilePath = projectFile.FullName;
                }
                
                if (projectFilePath != null)
                {
                    var project = new Project(projectRelativeFilePath!);
                    await ProjectReader.ReadProjectDetailsAsync(output, new FileInfo(projectFilePath), project);
                    
                    service.Service.Source = project;

                    // Apply defaults to everything that has a project.
                    var container = new ContainerInfo();
                    service.Service.GeneratedAssets.Container = container;
                    DockerfileGenerator.ApplyContainerDefaults(wrapper, service, project, container);
                }
            }

            output.WriteDebugLine($"Running {delegates.Count} customization callbacks.");
            for (var i = 0; i < delegates.Count; i++)
            {
                delegates[i].DynamicInvoke(application);
            }
            output.WriteDebugLine($"Done running {delegates.Count} customization callbacks.");


            return application;
        }

        public override void Register<TApplication>(Action<TApplication> callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (!callbacks.TryGetValue(typeof(TApplication), out var delegates))
            {
                delegates = new List<MulticastDelegate>();
                callbacks.Add(typeof(TApplication), delegates);
            }

            delegates.Add(callback);
        }

        private static ProjectInSolution? FindProjectInSolution(SolutionFile solution, string projectName)
        {
            for (var i = 0; i < solution.ProjectsInOrder.Count; i++)
            {
                var project = solution.ProjectsInOrder[i];
                var normalized = Names.NormalizeToFriendly(project.ProjectName);

                if (string.Equals(normalized, projectName, StringComparison.Ordinal) && 
                    project.AbsolutePath.EndsWith(".csproj", StringComparison.Ordinal))
                {
                    return project;
                }
            }

            return null;
        }
    }
}