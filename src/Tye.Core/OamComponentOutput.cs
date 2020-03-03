using System;
using YamlDotNet.RepresentationModel;

namespace Tye
{
    internal sealed class OamComponentOutput : ServiceOutput, IYamlManifestOutput
    {
        public OamComponentOutput(string name, YamlDocument yaml)
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
