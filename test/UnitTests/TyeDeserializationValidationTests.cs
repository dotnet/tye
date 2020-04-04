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
            Assert.Contains(CoreStrings.MultipleIngressBindingWithoutName, exception.Message);
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
      - port: 8080
        protocol: http";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(CoreStrings.MultipleServiceBindingsWithoutName, exception.Message);
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
            Assert.Contains(CoreStrings.MultipleIngressBindingWithSameName, exception.Message);
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
            Assert.Contains(CoreStrings.MultipleServiceBindingsWithSameName, exception.Message);
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
    }
}
