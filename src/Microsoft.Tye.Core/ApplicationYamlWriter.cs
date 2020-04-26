// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    public static class ApplicationYamlWriter
    {
        public static Task WriteAsync(OutputContext output, StreamWriter writer, ApplicationBuilder application)
        {
            var yaml = new List<IYamlManifestOutput>();

            yaml.AddRange(application.Services.SelectMany(s => s.Outputs.OfType<IYamlManifestOutput>()));
            yaml.AddRange(application.Ingress.SelectMany(i => i.Outputs.OfType<IYamlManifestOutput>()));

            if (yaml.Count == 0)
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
