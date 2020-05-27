// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public class PublishPorterStep : ApplicationExecutor.ApplicationStep
    {
        public override string DisplayText => "Publishing Porter Bundle ...";

        public string Environment { get; set; } = "production";

        public override async Task ExecuteAsync(OutputContext output, ApplicationBuilder application)
        {
            var dockerImages = application.Services.SelectMany(s => s.Outputs.OfType<DockerImageOutput>());
            // TODO Check if existing porter.yaml file exists.
            var document = PorterManifestGenerator.CreatePorterManifest(output, dockerImages, application);

            await PorterPublisher.PublishAsync(output, document, application); 
        }
    }
}
