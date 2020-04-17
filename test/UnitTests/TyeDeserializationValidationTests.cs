using System;
using System.Collections.Generic;
using System.Text;
using Tye;
using Tye.Serialization;
using Xunit;

namespace Microsoft.Tye.UnitTests
{
    public class TyeDeserializationValidationTests
    {
        [Fact]
        public void MultipleIngressBindingsMustHaveNames()
        {
            var input = @"
ingress:
  - name: ingress
    bindings:
      - port: 8080
        protocol: http
      - port: 8080
        protocol: http";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.FormatMultipleBindingWithoutName("ingress"), exception.Message);
        }

        [Fact]
        public void MultipleServicesBindingsMustHaveNames()
        {
            var input = @"
services:
  - name: app
    bindings:
      - port: 8080
        protocol: http
      - port: 8081
        protocol: http";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.FormatMultipleBindingWithoutName("service"), exception.Message);
        }

        [Fact]
        public void MultipleIngressBindingsMustUniqueNames()
        {
            var input = @"
ingress:
  - name: ingress
    bindings:
      - port: 8080
        protocol: http
        name: a
      - port: 8080
        protocol: http
        name: a";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.FormatMultipleBindingWithSameName("ingress"), exception.Message);
        }


        [Fact]
        public void IngressProtocolsShouldBeHttpOrHttps()
        {
            var input = @"
ingress:
  - name: ingress
    bindings:
      - port: 8080
        protocol: tls
        name: a";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.IngressBindingMustBeHttpOrHttps, exception.Message);
        }

        [Fact]
        public void MultipleServicesBindingsMustUniqueNames()
        {
            var input = @"
services:
  - name: app
    bindings:
      - port: 8080
        protocol: http
        name: a
      - port: 8080
        protocol: http
        name: a";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.FormatMultipleBindingWithSameName("service"), exception.Message);
        }

        [Fact]
        public void IngressMustReferenceService()
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
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.IngressRuleMustReferenceService, exception.Message);
        }

        [Fact]
        public void IngressMustHaveUniquePorts()
        {
            var input = @"
ingress:
  - name: ingress
    bindings:
      - port: 8080
        protocol: http
        name: foo
      - port: 8080
        protocol: https
        name: bar";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.FormatMultipleBindingWithSamePort("ingress"), exception.Message);
        }


        [Fact]
        public void ServicesMustHaveUniquePorts()
        {
            var input = @"
services:
  - name: app
    bindings:
      - port: 8080
        protocol: http
        name: a
      - port: 8080
        protocol: https
        name: b";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.FormatMultipleBindingWithSamePort("service"), exception.Message);
        }

        [Fact]
        public void ServicesMustHaveUniqueNonNullPorts()
        {
            var input = @"
services:
  - name: app
    bindings:
      - protocol: http
        name: a
      - protocol: https
        name: b";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            app.Validate();
        }


        [Theory]
        [InlineData("image", "executable")]
        [InlineData("project", "image")]
        [InlineData("project", "executable")]
        public void ImageExeProjectMutuallyExclusive(string a, string b)
        {
            var input = @$"
services:
  - name: app
    {a}: foo
    {b}: baz
    bindings:
      - port: 8080
        protocol: http";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.FormatProjectImageExecutableExclusive(a, b), exception.Message);
        }
    }
}
