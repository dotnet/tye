using System.IO;
using System.Linq;
using Microsoft.Tye.ConfigModel;
using Test.Infrastructure;
using Tye;
using Tye.Serialization;
using Xunit;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.Tye.UnitTests
{
    public class TyeDeserializationTests
    {
        private IDeserializer _deserializer;

        public TyeDeserializationTests()
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        [Fact]
        public void ComprehensionalTest()
        {
            var input = @"
name: apps-with-ingress
registry: myregistry
extensions:
  - name: dapr
ingress:
  - name: ingress
    bindings:
      - port: 8080
        protocol: http
        name: foo
    rules:
      - path: /A
        service: appA
      - path: /B
        service: appB
      - host: a.example.com
        service: appA
      - host: b.example.com
        service: appB
    replicas: 2
services:
  - name: appA
    project: ApplicationA/ApplicationA.csproj
    buildProperties:
    - name: Configuration
    - value: Debug
    replicas: 2
    tags:
      - tagA
      - tagC
    external: false
    image: abc
    build: false
    executable: test.exe
    workingDirectory: ApplicationA/
    args: a b c
    env:
    - name: POSTGRES_PASSWORD
      value: ""test""
    - name: POSTGRES_PASSWORD2
      value: ""test2""
    volumes:
    - name: volume
      source: /data
      target: /data
    bindings:
    - name: test
      port: 4444
      connectionString: asdf
      containerPort: 80
      host: localhost
      protocol: http
  - name: appB
    project: ApplicationB/ApplicationB.csproj
    replicas: 2
    tags:
      - tagB
      - tagD";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
        }

        [Fact]
        public void IngressIsSetCorrectly()
        {
            var input = @"
ingress:
  - name: ingress
    bindings:
      - port: 8080
        protocol: http
        name: foo
    rules:
      - path: /A
        service: appA
      - path: /B
        service: appB
      - host: a.example.com
        service: appA
      - host: b.example.com
        service: appB
    replicas: 2";

            using var parser = new YamlParser(input);
            var actual = parser.ParseConfigApplication();

            var expected = _deserializer.Deserialize<ConfigApplication>(new StringReader(input));

            TyeAssert.Equal(expected, actual);
        }

        [Fact]
        public void ServicesSetCorrectly()
        {
            var input = @"services:
  - name: appA
    project: ApplicationA/ApplicationA.csproj
    replicas: 2
    tags:
      - A
      - B
    external: false
    image: abc
    build: false
    executable: test.exe
    workingDirectory: ApplicationA/
    args: a b c
    env:
    - name: POSTGRES_PASSWORD
      value: ""test""
    - name: POSTGRES_PASSWORD2
      value: ""test2""
    volumes:
    - name: volume
      source: /data
      target: /data
    bindings:
    - name: test
      port: 4444
      connectionString: asdf
      containerPort: 80
      host: localhost
      protocol: http
  - name: appB
    project: ApplicationB/ApplicationB.csproj
    replicas: 2
    tags:
      - tC
      - tD";
            using var parser = new YamlParser(input);
            var actual = parser.ParseConfigApplication();

            var expected = _deserializer.Deserialize<ConfigApplication>(new StringReader(input));

            TyeAssert.Equal(expected, actual);
        }

        [Fact]
        public void ExtensionsTest()
        {
            var input = @"
extensions:
  - name: dapr";
            using var parser = new YamlParser(input);

            var app = parser.ParseConfigApplication();

            Assert.Equal("dapr", app.Extensions.Single()["name"]);

            var expected = _deserializer.Deserialize<ConfigApplication>(new StringReader(input));

            Assert.Equal(expected.Extensions.Count, app.Extensions.Count);
        }

        [Fact]
        public void NetworkTest()
        {
            var input = @"
network: test-network";
            using var parser = new YamlParser(input);

            var app = parser.ParseConfigApplication();

            Assert.Equal("test-network", app.Network);

            var expected = _deserializer.Deserialize<ConfigApplication>(new StringReader(input));

            Assert.Equal(expected.Network, app.Network);
        }

        [Fact]
        public void VotingTest()
        {
            using var parser = new YamlParser(
@"name: VotingSample
registry: myregistry
services:
- name: vote
  project: vote/vote.csproj
- name: redis
  image: redis
  bindings:
    - port: 6379
- name: worker
  project: worker/worker.csproj
- name: postgres
  image:  postgres
  env:
    - name: POSTGRES_PASSWORD
      value: ""test""
  bindings:
    - port: 5432
- name: results
  project: results/results.csproj");
            var app = parser.ParseConfigApplication();
        }


        [Fact]
        public void UnrecognizedConfigApplicationField_ThrowException()
        {
            using var parser = new YamlParser("asdf: 123");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnrecognizedKey("asdf"), exception.Message);
        }

        [Fact]
        public void Replicas_MustBeInteger()
        {
            using var parser = new YamlParser(
@"services:
- name: app
  replicas: asdf");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBeAnInteger("replicas"), exception.Message);
        }

        [Fact]
        public void Replicas_MustBePositive()
        {
            using var parser = new YamlParser(
@"services:
- name: app
  replicas: -1");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBePositive("replicas"), exception.Message);
        }

        [Fact]
        public void Name_MustBeScalar()
        {
            using var parser = new YamlParser(
@"name:
- a: b");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlScalar("name"), exception.Message);
        }


        [Fact]
        public void YamlIsCaseSensitive()
        {
            using var parser = new YamlParser(
@"Name: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnrecognizedKey("Name"), exception.Message);
        }

        [Fact]
        public void Registry_MustBeScalar()
        {
            using var parser = new YamlParser(
@"registry:
- a: b");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlScalar("registry"), exception.Message);
        }

        [Fact]
        public void Ingress_MustBeSequence()
        {
            using var parser = new YamlParser(
@"ingress: a");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("ingress"), exception.Message);
        }

        [Fact]
        public void Services_MustBeSequence()
        {
            using var parser = new YamlParser(
@"services: a");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("services"), exception.Message);
        }

        [Fact]
        public void ConfigApplication_MustBeMappings()
        {
            using var parser = new YamlParser(
@"- name: app
  replicas: -1");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), YamlNodeType.Sequence.ToString()), exception.Message);
        }

        [Fact]
        public void Services_MustBeMappings()
        {
            using var parser = new YamlParser(
@"services:
  - name");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), YamlNodeType.Scalar.ToString()), exception.Message);
        }

        [Fact]
        public void Ingress_MustBeMappings()
        {
            using var parser = new YamlParser(
@"ingress:
  - name");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), YamlNodeType.Scalar.ToString()), exception.Message);
        }

        [Fact]
        public void Ingress_Replicas_MustBeInteger()
        {
            using var parser = new YamlParser(
@"ingress:
  - replicas: asdf");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBeAnInteger("replicas"), exception.Message);
        }

        [Fact]
        public void Ingress_Replicas_MustBePositive()
        {
            using var parser = new YamlParser(
@"ingress:
  - replicas: -1");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBePositive("replicas"), exception.Message);
        }

        [Fact]
        public void Ingress_UnrecognizedKey()
        {
            using var parser = new YamlParser(
@"ingress:
  - abc: abc");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnrecognizedKey("abc"), exception.Message);
        }

        [Fact]
        public void Ingress_Rules_MustSequence()
        {
            using var parser = new YamlParser(
@"ingress:
  - rules: abc");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("rules"), exception.Message);
        }

        [Fact]
        public void Ingress_Rules_MustBeMappings()
        {
            using var parser = new YamlParser(
@"ingress:
  - rules:
    - abc");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), YamlNodeType.Scalar.ToString()), exception.Message);
        }

        [Fact]
        public void Ingress_Bindings_MustBeMappings()
        {
            using var parser = new YamlParser(
@"ingress:
  - bindings:
    - abc");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnexpectedType(YamlNodeType.Mapping.ToString(), YamlNodeType.Scalar.ToString()), exception.Message);
        }

        [Fact]
        public void Ingress_RulesMapping_UnrecognizedKey()
        {
            using var parser = new YamlParser(
@"ingress:
  - rules:
    - abc: 123");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnrecognizedKey("abc"), exception.Message);
        }

        [Fact]
        public void Ingress_Bindings_MustSequence()
        {
            using var parser = new YamlParser(
@"ingress:
  - bindings: abc");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("bindings"), exception.Message);
        }

        [Fact]
        public void Ingress_Bindings_Port_MustBeInteger()
        {
            using var parser = new YamlParser(
@"ingress:
  - name: ingress
    bindings:
      - port: abc
        protocol: http
        name: foo");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBeAnInteger("port"), exception.Message);
        }

        [Fact]
        public void Ingress_Bindings_UnrecognizedKey()
        {
            using var parser = new YamlParser(
@"ingress:
  - name: ingress
    bindings:
      - abc: abc");
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnrecognizedKey("abc"), exception.Message);
        }

        [Fact]
        public void Services_External_MustBeBool()
        {
            using var parser = new YamlParser(
@"services:
  - name: ingress
    external: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBeABoolean("external"), exception.Message);
        }

        [Fact]
        public void Services_Build_MustBeBool()
        {
            using var parser = new YamlParser(
@"services:
  - name: ingress
    build: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBeABoolean("build"), exception.Message);
        }

        [Fact]
        public void Services_Bindings_MustBeSequence()
        {
            using var parser = new YamlParser(
@"services:
  - name: ingress
    bindings: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("bindings"), exception.Message);
        }


        [Fact]
        public void Services_Volumes_MustBeSequence()
        {
            using var parser = new YamlParser(
@"services:
  - name: ingress
    volumes: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("volumes"), exception.Message);
        }

        [Fact]
        public void Services_Env_MustBeSequence()
        {
            using var parser = new YamlParser(
@"services:
  - name: ingress
    env: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("env"), exception.Message);
        }

        [Fact]
        public void Services_Tags_MustBeSequence()
        {
            using var parser = new YamlParser(
@"services:
  - name: ingress
    tags: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("tags"), exception.Message);
        }

        [Fact]
        public void Services_Tags_SetCorrectly()
        {
            var input = @"
services:
  - name: ingress
    tags:
      - tagA
      - with space
      - ""C.X""
";
            using var parser = new YamlParser(input);
            var actual = parser.ParseConfigApplication();

            var expected = _deserializer.Deserialize<ConfigApplication>(new StringReader(input));

            TyeAssert.Equal(expected, actual);
        }

        [Fact]
        public void Services_UnrecognizedKey()
        {
            using var parser = new YamlParser(
@"services:
  - name: ingress
    xyz: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnrecognizedKey("xyz"), exception.Message);
        }

        [Theory]
        [InlineData("liveness")]
        [InlineData("readiness")]
        public void Probe_UnrecognizedKey(string probe)
        {
            using var parser = new YamlParser($@"
services:
    - name: sample
      {probe}:
        something: something");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnrecognizedKey("something"), exception.Message);
        }

        [Theory]
        [InlineData("initialDelay")]
        [InlineData("period")]
        [InlineData("timeout")]
        [InlineData("successThreshold")]
        [InlineData("failureThreshold")]
        public void Probe_ScalarFields_MustBeInteger(string field)
        {
            using var parser = new YamlParser($@"
services:
    - name: sample
      liveness:
        {field}: 3.5");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBeAnInteger(field), exception.Message);
        }

        [Theory]
        [InlineData("initialDelay")]
        public void Probe_ScalarFields_MustBePositive(string field)
        {
            using var parser = new YamlParser($@"
services:
    - name: sample
      liveness:
        {field}: -1");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBePositive(field), exception.Message);
        }

        [Theory]
        [InlineData("period")]
        [InlineData("timeout")]
        [InlineData("successThreshold")]
        [InlineData("failureThreshold")]
        public void Probe_ScalarFields_MustBeGreaterThanZero(string field)
        {
            using var parser = new YamlParser($@"
services:
    - name: sample
      liveness:
        {field}: 0");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBeGreaterThanZero(field), exception.Message);
        }

        [Fact]
        public void Probe_HttpProber_UnrecognizedKey()
        {
            using var parser = new YamlParser(@"
services:
    - name: sample
      liveness:
        http:
            something: something");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnrecognizedKey("something"), exception.Message);
        }

        [Fact]
        public void Probe_HttpProber_PortMustBeScalar()
        {
            using var parser = new YamlParser($@"
services:
    - name: sample
      liveness:
        http:
            port: 3.5");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatMustBeAnInteger("port"), exception.Message);
        }

        [Fact]
        public void Probe_HttpProber_HeadersMustBeSequence()
        {
            using var parser = new YamlParser(@"
services:
    - name: sample
      liveness:
        http:
            headers: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("headers"), exception.Message);
        }

        [Fact]
        public void Probe_HttpProber_Headers_UnrecognizedKey()
        {
            using var parser = new YamlParser(@"
services:
    - name: sample
      liveness:
        http:
            headers:
                - name: header1
                  something: something");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatUnrecognizedKey("something"), exception.Message);
        }
    }
}
