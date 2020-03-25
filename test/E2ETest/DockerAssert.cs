// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace E2ETest
{
    public static class DockerAssert
    {
        // Repository is the "registry/image" format. Yeah Docker uses that term for it, and it's 
        // wierd and confusing.
        public static async Task AssertImageExistsAsync(ITestOutputHelper output, string repository)
        {
            var builder = new StringBuilder();

            output.WriteLine($"> docker images \"{repository}\" --format \"{{{{.Repository}}}}\"");
            var exitCode = await Process.ExecuteAsync(
                "docker",
                $"images \"{repository}\" --format \"{{{{.Repository}}}}\"",
                stdOut: OnOutput,
                stdErr: OnOutput);
            if (exitCode != 0)
            {
                throw new XunitException($"Running `docker images \"{repository}\"` failed." + Environment.NewLine + builder.ToString());
            }

            var lines = builder.ToString().Split(new[] { '\r', '\n', }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Any(line => line == repository))
            {
                return;
            }

            throw new XunitException($"Image '{repository}' was not found.");

            void OnOutput(string text)
            {
                builder.AppendLine(text);
                output.WriteLine(text);
            }
        }

        public static async Task DeleteDockerImagesAsync(ITestOutputHelper output, string repository)
        {
            var ids = await ListDockerImagesIdsAsync(output, repository);

            var builder = new StringBuilder();

            foreach (var id in ids)
            {

                output.WriteLine($"> docker rmi \"{id}\" --force");
                var exitCode = await Process.ExecuteAsync(
                    "docker",
                    $"rmi \"{id}\" --force",
                    stdOut: OnOutput,
                    stdErr: OnOutput);
                if (exitCode != 0)
                {
                    throw new XunitException($"Running `docker rmi \"{id}\" --force` failed." + Environment.NewLine + builder.ToString());
                }

                builder.Clear();
            }

            void OnOutput(string text)
            {
                builder.AppendLine(text);
                output.WriteLine(text);
            }
        }

        public static async Task<string[]> GetRunningContainersIdsAsync(ITestOutputHelper output)
        {
            var builder = new StringBuilder();

            output.WriteLine($"> docker ps --format \"{{{{.ID}}}}\"");
            var exitCode = await Process.ExecuteAsync(
                "docker",
                $"ps --format \"{{{{.ID}}}}\"",
                stdOut: OnOutput,
                stdErr: OnOutput);
            if (exitCode != 0)
            {
                throw new XunitException($"Running `docker ps` failed." + Environment.NewLine + builder.ToString());
            }

            var lines = builder.ToString().Split(new[] { '\r', '\n', }, StringSplitOptions.RemoveEmptyEntries);
            return lines;

            void OnOutput(string text)
            {
                builder.AppendLine(text);
                output.WriteLine(text);
            }
        }

        private static async Task<string[]> ListDockerImagesIdsAsync(ITestOutputHelper output, string repository)
        {
            // docker images -q '{repository}' returns just the ID of the image (one per line)
            // It does not fail if there are no matches, just returns empty output.

            var builder = new StringBuilder();

            output.WriteLine($"> docker images -q \"{repository}\"");
            var exitCode = await Process.ExecuteAsync(
                "docker",
                $"images -q \"{repository}\"",
                stdOut: OnOutput,
                stdErr: OnOutput);
            if (exitCode != 0)
            {
                throw new XunitException($"Running `docker images -q \"{repository}\"` failed." + Environment.NewLine + builder.ToString());
            }

            var lines = builder.ToString().Split(new[] { '\r', '\n', }, StringSplitOptions.RemoveEmptyEntries);
            return lines;

            void OnOutput(string text)
            {
                builder.AppendLine(text);
                output.WriteLine(text);
            }
        }
    }
}
