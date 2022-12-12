using System;
using System.IO;
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
            Assert.Throws<TyeYamlException>(() => app.Validate());
        }

        [Fact]
        public void ValidateServiceNameThrowsException()
        {
            var input = @"
services:
  - name: app_
    bindings:
      - protocol: http
        name: a
      - protocol: https
        name: b";
            var errorMessage = "A service name must consist of lower case alphanumeric";
            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(errorMessage, exception.Message);
        }

        [Fact]
        public void ValidateServiceNameThrowsExceptionForMaxLength()
        {
            var input = @"
services:
  - name: appavalidateservicenamethrowsexceptionformaxlengthvalidateservicen
    bindings:
      - protocol: http
        name: a
      - protocol: https
        name: b";
            var errorMessage = "Name cannot be more that 63 characters long.";
            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(errorMessage, exception.Message);
        }

        [Fact]
        public void ProberRequired()
        {
            var input = @"
services:
    - name: sample
      liveness:
        period: 1";
            var errorMessage = CoreStrings.FormatProberRequired("liveness");
            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(errorMessage, exception.Message);
        }

        [Fact]
        public void LivenessProbeSuccessThresholdMustBeOne()
        {
            var input = @"
services:
    - name: sample
      liveness:
        successThreshold: 2
        http:
            path: /";
            var errorMessage = CoreStrings.FormatSuccessThresholdMustBeOne("liveness");
            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            var exception = Assert.Throws<TyeYamlException>(() => app.Validate());
            Assert.Contains(errorMessage, exception.Message);
        }

        [Fact]
        public void BadYmlFileWithArgs_ThrowsExceptionWithUsefulFilePath()
        {
            var input = @"
flimflam";

            using var parser = new YamlParser(input, new FileInfo("foobar.yml"));
            try
            {
                parser.ParseConfigApplication();
                Assert.False(true, "YML parsing exception expected with supplied input");
            }
            catch (TyeYamlException e)
            {
                Assert.StartsWith("Error parsing 'foobar.yml': (2, 1): Unexpected node type in the tye configuration file.", e.Message);
            }
        }

        [Fact]
        public void DockerFileWithArgs()
        {
            var input = @"
services:
    - name: web
      dockerFile: Dockerfile
      dockerFileArgs: 
      - pat: thisisapat";

            using var parser = new YamlParser(input);
            var app = parser.ParseConfigApplication();
            app.Validate();
        }
    }
}
