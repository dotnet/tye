using YamlDotNet.RepresentationModel;

namespace Tye
{
    internal interface IYamlManifestOutput
    {
        YamlDocument Yaml { get; }
    }
}
