using System.IO;
using Microsoft.Tye.ConfigModel;
using Microsoft.Tye.Serialization;
using Tye.Serialization;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace UnitTests
{
    public class TyeDeserializationTests
    {
        [Fact]
        public void ParseProjects()
        {
            var input = "name: temp";
            var parser = YamlSerializer.CreateDeserializer();
            var result = parser.Deserialize<ConfigApplication>(input);
        }


        [Fact]
        public void ParseInvalidName()
        {
            var input = "abc: temp";
            var parser = YamlSerializer.CreateDeserializer();
            var result = Assert.Throws<YamlException>(() => parser.Deserialize<ConfigApplication>(input));
        }

        [Fact]
        public void Testing()
        {
            var parser = new YamlParser(
@"# tye application configuration file
# read all about it at https://github.com/dotnet/tye
name: single-project
services:
- name: test-project
  project: test-project/test-project.csproj");
            var app = parser.GetConfigApplication();
        }

        [Fact]
        public void MultipleProjectsSameName()
        {
            var parser = new YamlParser(
@"# tye application configuration file
# read all about it at https://github.com/dotnet/tye
name: SoManyProjects
services:
- name: test-project
  project: test-project/test-project.csproj
- name: test-project
  project: test-project/test-project.csproj
- name: test-project
  project: test-project/test-project.csproj
- name: test-project
  project: test-project/test-project.csproj
- name: test-project
  project: test-project/test-project.csproj");
            var app = parser.GetConfigApplication();
        }
    }
}
