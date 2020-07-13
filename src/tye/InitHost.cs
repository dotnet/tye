﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Tye.ConfigModel;
using Microsoft.Tye.Serialization;

namespace Microsoft.Tye
{
    public static class InitHost
    {
        public static string CreateTyeFile(FileInfo? path, bool force)
        {
            var (content, outputFilePath) = CreateTyeFileContent(path, force);

            File.WriteAllText(outputFilePath, content);

            return outputFilePath;
        }

        public static (string, string) CreateTyeFileContent(FileInfo? path, bool force)
        {
            if (path is FileInfo && path.Exists && !force)
            {
                ThrowIfTyeFilePresent(path, "tye.yml");
                ThrowIfTyeFilePresent(path, "tye.yaml");
            }

            var template = @"
# tye application configuration file
# read all about it at https://github.com/dotnet/tye
#
# when you've given us a try, we'd love to know what you think:
#    https://aka.ms/AA7q20u
#
# define global settings here
# name: exampleapp # application name
# registry: exampleuser # dockerhub username or container registry hostname

# define multiple services here
services:
- name: myservice
  # project: app.csproj # msbuild project path (relative to this file)
  # executable: app.exe # path to an executable (relative to this file)
  # args: --arg1=3 # arguments to pass to the process
  # replicas: 5 # number of times to launch the application
  # env: # array of environment variables
  #  - name: key
  #    value: value
  # bindings: # optional array of bindings (ports, connection strings)
    # - port: 8080 # number port of the binding
".TrimStart();

            // Output in the current directory unless an input file was provided, then
            // output next to the input file.
            var outputFilePath = "tye.yaml";

            if (path is FileInfo && path.Exists)
            {
                var application = ConfigFactory.FromFile(path);
                var serializer = YamlSerializer.CreateSerializer();

                var extension = path.Extension.ToLowerInvariant();
                var directory = path.Directory;

                // Clear all bindings if any for solutions and project files
                if (extension == ".sln" || extension == ".csproj" || extension == ".fsproj")
                {
                    // If the input file is a project or solution then use that as the name
                    application.Extensions = null!;
                    application.Ingress = null!;

                    foreach (var service in application.Services)
                    {
                        service.Bindings = null!;
                        service.Configuration = null!;
                        service.Volumes = null!;
                        service.Project = service.Project!.Substring(directory.FullName.Length).TrimStart('/');
                    }

                    // If the input file is a sln/project then place the config next to it
                    outputFilePath = Path.Combine(directory.FullName, "tye.yaml");
                }
                else
                {
                    // If the input file is a yaml, then replace it.
                    outputFilePath = path.FullName;
                }

                template = @"
# tye application configuration file
# read all about it at https://github.com/dotnet/tye
#
# when you've given us a try, we'd love to know what you think:
#    https://aka.ms/AA7q20u
#
".TrimStart() + serializer.Serialize(application);
            }

            AddToSolutionItems(path);

            return (template, outputFilePath);
        }

        private static void ThrowIfTyeFilePresent(FileInfo? path, string yml)
        {
            var tyeYaml = Path.Combine(path!.DirectoryName, yml);
            if (File.Exists(tyeYaml))
            {
                throw new CommandException($"File '{tyeYaml}' already exists. Use --force to override the {yml} file if desired.");
            }
        }

        private static void AddToSolutionItems(FileInfo? path)
        {
            var slnFiles = Directory.GetFiles(path!.DirectoryName, "*.sln");
            if (slnFiles.Length == 1)
            {
                var sln = slnFiles[0];

                var lines = File.ReadAllLines(sln).ToList<string>();
                var insertAt = 0;
                var exists = false;

                lines.ForEach(l =>
                {
                    if (l.IndexOf("(SolutionItems)") > 0)
                        exists = true;

                    if (!exists)
                        insertAt++;
                });

                var insertItem = string.Empty;
                if (exists)
                {
                    insertItem = "        tye.yaml = tye.yaml";
                    lines.Insert(insertAt + 1, insertItem);
                }
                else
                {
                    var fileGuid = Guid.NewGuid().ToString("B").ToUpper();

                    insertItem = @$"Project(""{fileGuid}"") = ""Solution Items"", ""Solution Items"", ""{fileGuid}""
    ProjectSection(SolutionItems) = preProject
        tye.yaml = tye.yaml
    EndProjectSection
EndProject";

                    insertAt = lines.LastIndexOf("EndProject");
                    lines.Insert(insertAt + 1, insertItem);
                }

                File.WriteAllLines(sln, lines.ToArray());
            }
        }
    }
}
