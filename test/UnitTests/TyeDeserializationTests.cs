using Tye;
using Tye.Serialization;
using Xunit;

namespace UnitTests
{
    public class TyeDeserializationTests
    {
        [Fact]
        public void BasicServiceAndNameProject()
        {
            var parser = new YamlParser(
@"name: single-project
services:
- name: test-project
  project: test-project/test-project.csproj");
            var app = parser.ParseConfigApplication();
        }

        [Fact]
        public void MultipleProjectsSameNameParsed_NotValidated()
        {
            var parser = new YamlParser(
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

        [Fact]
        public void IngressTest()
        {
            var parser = new YamlParser(
@"name: apps-with-ingress
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
            var parser = new YamlParser(
@"name: VotingSample
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
        public void UnrecognizedField_Ignored()
        {
            var parser = new YamlParser("asdf: 123");
            var app = parser.ParseConfigApplication();
        }


        [Fact]
        public void Replicas_MustBeInteger()
        {
            var parser = new YamlParser(
@"services:
- name: app
  replicas: asdf");
            //figure out how kestrel does exceptions
            var exception = Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
            Assert.Equal(CoreStrings.FormatScalarValueRequired(""), exception.Message);
        }

        [Fact]
        public void Replicas_MustBePositive()
        {
            var parser = new YamlParser(
@"services:
- name: app
  replicas: -1");
            Assert.Throws<TyeYamlException>(() => parser.ParseConfigApplication());
        }
    }
}
