using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.RepresentationModel;

namespace E2ETest
{
    public static class YamlAssert
    {
        public static void Equals(string expected, string actual, ITestOutputHelper output = null!)
        {
            var yamlStream = new YamlStream();
            using var reader = new StringReader(expected);
            yamlStream.Load(reader);

            var otherYamlStream = new YamlStream();
            using var otherReader = new StringReader(expected);
            otherYamlStream.Load(new StringReader(actual));

            var yamlEqualityVisitor = new EqualityYamlNodeVisitor();

            try
            {
                Assert.Equal(yamlStream.Documents.Count, yamlStream.Documents.Count);

                for (var i = 0; i < yamlStream.Documents.Count; i++)
                {
                    yamlEqualityVisitor.Visit(yamlStream.Documents[i].RootNode, otherYamlStream.Documents[i].RootNode);
                }
            }
            catch (Exception)
            {
                output?.WriteLine("Expected:");
                output?.WriteLine(expected);
                output?.WriteLine("Actual:");
                output?.WriteLine(actual);

                throw;
            }
        }
    }
}
