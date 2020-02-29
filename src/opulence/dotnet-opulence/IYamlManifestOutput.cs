using YamlDotNet.RepresentationModel;

namespace Opulence
{
    internal interface IYamlManifestOutput
    {
        YamlDocument Yaml { get; }
    }
}
