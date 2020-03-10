// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
            if (repository is null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            var builder = new StringBuilder();

            var exitCode = await Process.ExecuteAsync(
                "docker",
                $"images \"{repository}\" --format \"{{{{.Repository}}}}\"",
                stdOut: OnOutput,
                stdErr: OnOutput);
            if (exitCode != 0)
            {
                throw new XunitException($"Running `docker images \"{repository}\"` failed." + Environment.NewLine + builder.ToString());
            }

            var lines = builder.ToString().Split(new[] { '\r', '\n', });
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
    }
}
