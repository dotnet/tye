using System;
using Microsoft.Tye.ConfigModel;
using Microsoft.Tye.Serialization;
using Xunit;

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
    }
}
