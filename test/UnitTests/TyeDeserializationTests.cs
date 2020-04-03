using System.IO;
using System.Linq;
using Microsoft.Tye.ConfigModel;
using Tye;
using Tye.Serialization;
using Xunit;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace UnitTests
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
    replicas: 2
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
    replicas: 2";

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
            var app = parser.ParseConfigApplication();

            var expected = _deserializer.Deserialize<ConfigApplication>(new StringReader(input));

            foreach (var ingress in app.Ingress)
            {
                var otherIngress = expected
                    .Ingress
                    .Where(o => o.Name == ingress.Name)
                    .Single();
                Assert.NotNull(otherIngress);
                Assert.Equal(otherIngress.Replicas, ingress.Replicas);

                foreach (var rule in ingress.Rules)
                {
                    var otherRule = otherIngress
                        .Rules
                        .Where(o => o.Path == rule.Path && o.Host == rule.Host && o.Service == rule.Service)
                        .Single();
                    Assert.NotNull(otherRule);
                }

                foreach (var binding in ingress.Bindings)
                {
                    var otherBinding = otherIngress
                        .Bindings
                        .Where(o => o.Name == binding.Name && o.Port == binding.Port && o.Protocol == binding.Protocol)
                        .Single();

                    Assert.NotNull(otherBinding);
                }
            }
        }

        [Fact]
        public void ServicesSetCorrectly()
        {
            var input = @"services:
  - name: appA
    project: ApplicationA/ApplicationA.csproj
    replicas: 2
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
    replicas: 2";
            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();

            var expected = _deserializer.Deserialize<ConfigApplication>(new StringReader(input));

            foreach (var service in app.Services)
            {
                var otherService = expected
                    .Services
                    .Where(o => o.Name == service.Name)
                    .Single();
                Assert.NotNull(otherService);
                Assert.Equal(otherService.Args, service.Args);
                Assert.Equal(otherService.Build, service.Build);
                Assert.Equal(otherService.Executable, service.Executable);
                Assert.Equal(otherService.External, service.External);
                Assert.Equal(otherService.Image, service.Image);
                Assert.Equal(otherService.Project, service.Project);
                Assert.Equal(otherService.Replicas, service.Replicas);
                Assert.Equal(otherService.WorkingDirectory, service.WorkingDirectory);

                foreach (var binding in service.Bindings)
                {
                    var otherBinding = otherService.Bindings
                                    .Where(o => o.Name == binding.Name
                                        && o.Port == binding.Port
                                        && o.Protocol == binding.Protocol
                                        && o.ConnectionString == binding.ConnectionString
                                        && o.ContainerPort == binding.ContainerPort
                                        && o.Host == binding.Host)
                                    .Single();

                    Assert.NotNull(otherBinding);
                }

                foreach (var binding in service.Bindings)
                {
                    var otherBinding = otherService.Bindings
                                    .Where(o => o.Name == binding.Name
                                        && o.Port == binding.Port
                                        && o.Protocol == binding.Protocol
                                        && o.ConnectionString == binding.ConnectionString
                                        && o.ContainerPort == binding.ContainerPort
                                        && o.Host == binding.Host)
                                    .Single();

                    Assert.NotNull(otherBinding);
                }

                foreach (var config in service.Configuration)
                {
                    var otherConfig = otherService.Configuration
                                    .Where(o => o.Name == config.Name
                                        && o.Value == config.Value)
                                    .Single();

                    Assert.NotNull(otherConfig);
                }

                foreach (var volume in service.Volumes)
                {
                    var otherVolume = otherService.Volumes
                                   .Where(o => o.Name == volume.Name
                                       && o.Target == volume.Target
                                       && o.Source == volume.Source)
                                   .Single();
                    Assert.NotNull(otherVolume);
                }
            }
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
        public void Services_UnrecognizedKey()
        {
            using var parser = new YamlParser(
@"services:
  - name: ingress
    env: abc");

            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Contains(CoreStrings.FormatExpectedYamlSequence("env"), exception.Message);
        }
    }
}
