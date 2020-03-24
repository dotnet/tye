// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    public sealed class ApplicationYamlWriter
    {
        public static Task WriteAsync(OutputContext output, StreamWriter writer, ApplicationBuilder application)
        {
            var yaml = application.Services.SelectMany(s => s.Outputs.OfType<IYamlManifestOutput>()).ToArray();
            if (yaml.Length == 0)
            {
                output.WriteDebugLine($"No yaml manifests found. Skipping.");
                return Task.CompletedTask;
            }

            var yamlStream = new YamlStream(yaml.Select(y => y.Yaml));
            yamlStream.Save(writer, assignAnchors: false);

            return Task.CompletedTask;
        }
    }
}
