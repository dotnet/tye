using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Tye;
using YamlDotNet.RepresentationModel;

namespace Microsoft.Tye
{
    public class PorterManifestGenerator
    {
        public static YamlDocument CreatePorterManifest(OutputContext output, IEnumerable<DockerImageOutput> dockerImages, ApplicationBuilder application)
        {
            // write to temp directory for this step, make sure we allow generating to current directory
            var root = new YamlMappingNode();
            root.Add("name", application.Name);
            root.Add("description", "test");
            // TODO versioning
            root.Add("version", "0.1.0");
            root.Add("tag", $"{application.Registry}/{application.Name}:0.1.0");

            if (dockerImages.Count() > 0)
            {
                var images = new YamlMappingNode();
                root.Add("images", images);
                foreach (var image in dockerImages)
                {
                    var mapping = new YamlMappingNode();
                    images.Add(image.ImageName, mapping);
                    mapping.Add("imageType", "docker");
                    mapping.Add("repository", image.ImageName); // TODO may need repository here.
                    mapping.Add("tag", image.ImageTag);
                }
            }

            return new YamlDocument(root);
        }
    }
}
