using Tye;
using Tye.Serialization;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace UnitTests
{
    public class TyeDeserializationTests
    {
        [Fact]
        public void BasicServiceAndNameProject()
        {
            using var parser = new YamlParser(
@"name: single-project
services:
- name: test-project
  project: test-project/test-project.csproj");
            var app = parser.ParseConfigApplication();
        }

        [Fact]
        public void IngressTest()
        {
            using var parser = new YamlParser(
@"name: apps-with-ingress
registry: myregistry
ingress:
  - name: ingress
    bindings:
      - port: 8080
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
  replicas: 2
- name: appB
  project: ApplicationB/ApplicationB.csproj
  replicas: 2");
            var app = parser.ParseConfigApplication();
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
        public void MultipleProjectsSameNameParsed_Parsed()
        {
            using var parser = new YamlParser(
@"name: SoManyProjects
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
            var app = parser.ParseConfigApplication();
        }
    }
}
