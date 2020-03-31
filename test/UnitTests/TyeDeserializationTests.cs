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
        public void Testing()
        {
            var parser = new YamlParser(
@"# tye application configuration file
# read all about it at https://github.com/dotnet/tye
name: single-project
services:
- name: test-project
  project: test-project/test-project.csproj");
            var app = parser.ParseConfigApplication();
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
    }
}
