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
            var mixins = new YamlSequenceNode();
            root.Add("mixins", mixins);
            mixins.Add("exec");

            var installSeq = new YamlSequenceNode();
            root.Add("install", installSeq);
            var installExec = new YamlMappingNode();
            installSeq.Add(installExec);
            var args = new YamlMappingNode();
            installExec.Add("exec", args);
            args.Add("command", "ls");
            args.Add("description", "ls");

            //root.Add("upgrade", "");
            var uninstallSeq = new YamlSequenceNode();
            root.Add("uninstall", uninstallSeq);
            var uninstallExec = new YamlMappingNode();
            uninstallSeq.Add(uninstallExec);
            var unargs = new YamlMappingNode();
            uninstallExec.Add("exec", unargs);
            unargs.Add("command", "ls");
            unargs.Add("description", "ls");

            return new YamlDocument(root);
        }
    }
}
