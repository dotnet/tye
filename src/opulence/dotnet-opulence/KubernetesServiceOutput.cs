using System;
using YamlDotNet.RepresentationModel;

namespace Opulence
{
    internal sealed class KubernetesServiceOutput : ServiceOutput, IYamlManifestOutput
    {
        public KubernetesServiceOutput(string name, YamlDocument yaml)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (yaml is null)
            {
                throw new ArgumentNullException(nameof(yaml));
            }

            Name = name;
            Yaml = yaml;
        }

        public string Name { get; }
        public YamlDocument Yaml { get; }
    }
}