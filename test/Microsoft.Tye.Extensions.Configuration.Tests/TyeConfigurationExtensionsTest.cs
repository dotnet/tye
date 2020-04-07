// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration.Memory;
using Xunit;

namespace Microsoft.Extensions.Configuration
{
    public class TyeConfigurationExtensionsTest
    {
        [Fact]
        public void GetConnectionString_WithoutBinding_GetsConnectionString()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "connectionstrings:myservice", "expected" }
            });

            var configuration = builder.Build();

            var result = configuration.GetConnectionString("myservice");
            Assert.Equal("expected", result);
        }

        [Fact]
        public void GetConnectionString_WithoutBinding_DoesNotCombineHostAndPort()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:myservice:host", "example.com" },
                { "service:myservice:port", "443" }
            });

            var configuration = builder.Build();

            var result = configuration.GetConnectionString("myservice");
            Assert.Null(result);
        }

        [Fact]
        public void GetConnectionString_WithBinding_GetsConnectionString()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "connectionstrings:myservice:http", "expected" }
            });

            var configuration = builder.Build();

            var result = configuration.GetConnectionString("myservice", "http");
            Assert.Equal("expected", result);
        }

        [Fact]
        public void GetConnectionString_WithBinding_DoesNotMatchOtherBinding()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "connectionstrings:myservice:https", "expected" }
            });

            var configuration = builder.Build();

            var result = configuration.GetConnectionString("myservice", "http");
            Assert.Null(result);
        }

        [Fact]
        public void GetConnectionString_WithBinding_DoesNotMatchDefaultBinding()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "connectionstrings:myservice", "expected" }
            });

            var configuration = builder.Build();

            var result = configuration.GetConnectionString("myservice", "http");
            Assert.Null(result);
        }

        [Fact]
        public void GetConnectionString_WithBinding_DoesNotCombineHostAndPort()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:myservice:http:host", "example.com" },
                { "service:myservice:http:port", "443" }
            });

            var configuration = builder.Build();

            var result = configuration.GetConnectionString("myservice", "http");
            Assert.Null(result);
        }

        [Fact]
        public void GetServiceUri_WithoutBinding_IgnoresConnectionStringIfSet()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "connectionstrings:myservice", "https://example.com" },
                { "service:myservice:protocol", "http" },
                { "service:myservice:host", "expected.example.com" },
                { "service:myservice:port", "80" },
            });

            var configuration = builder.Build();

            var result = configuration.GetServiceUri("myservice");
            Assert.Equal(new Uri("http://expected.example.com"), result);
        }

        [Fact]
        public void GetServiceUri_WithoutBinding_ReturnsNullIfNotFound()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:anotherservice:protocol", "https" },
                { "service:anotherservice:host", "expected.example.com" },
                { "service:anotherservice:port", "5000" },
            });

            var configuration = builder.Build();

            var result = configuration.GetServiceUri("myservice");
            Assert.Null(result);
        }

        [Fact]
        public void GetServiceUri_WithoutBinding_CombinesHostAndPort()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:myservice:protocol", "https" },
                { "service:myservice:host", "expected.example.com" },
                { "service:myservice:port", "5000" },
            });

            var configuration = builder.Build();

            var result = configuration.GetServiceUri("myservice");
            Assert.Equal(new Uri("https://expected.example.com:5000"), result);
        }

        [Fact]
        public void GetServiceUri_WithoutBinding_WithHttpAsDefaultProtocol()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:myservice:host", "expected.example.com" },
                { "service:myservice:port", "5000" },
            });

            var configuration = builder.Build();

            var result = configuration.GetServiceUri("myservice");
            Assert.Equal(new Uri("http://expected.example.com:5000"), result);
        }

        [Fact]
        public void GetServiceUri_WithoutBinding_WithoutHost_ReturnsNull()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:myservice:protocol", "https" },
                { "service:myservice:port", "5000" },
            });

            var configuration = builder.Build();

            var result = configuration.GetServiceUri("myservice");
            Assert.Null(result);
        }

        [Fact]
        public void GetServiceUri_WithoutBinding_WithoutPort_ReturnsNull()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:myservice:protocol", "https" },
                { "service:myservice:host", "example.com" },
            });

            var configuration = builder.Build();

            var result = configuration.GetServiceUri("myservice");
            Assert.Null(result);
        }

        [Fact]
        public void GetServiceUri_InvalidValues_Throws()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:myservice:protocol", "https" },
                { "service:myservice:host", "@#*$&$(" },
                { "service:myservice:port", "example.com" },
            });

            var configuration = builder.Build();

            Assert.Throws<UriFormatException>(() => configuration.GetServiceUri("myservice"));
        }

        [Fact]
        public void GetServiceUri_WithBinding_IgnoresConnectionStringIfSet()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "connectionstrings:myservice:metrics", "https://example.com" },
                { "service:myservice:metrics:protocol", "http" },
                { "service:myservice:metrics:host", "expected.example.com" },
                { "service:myservice:metrics:port", "80" },
            });

            var configuration = builder.Build();

            var result = configuration.GetServiceUri("myservice", "metrics");
            Assert.Equal(new Uri("http://expected.example.com"), result);
        }

        [Fact]
        public void GetServiceUri_WithBinding_CombinesHostAndPort()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:myservice:metrics:protocol", "https" },
                { "service:myservice:metrics:host", "expected.example.com" },
                { "service:myservice:metrics:port", "5000" },
            });

            var configuration = builder.Build();

            var result = configuration.GetServiceUri("myservice", "metrics");
            Assert.Equal(new Uri("https://expected.example.com:5000"), result);
        }

        [Fact]
        public void GetServiceUri_WithBinding_IgnoresDefaultBinding()
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "service:myservice:host", "expected.example.com" },
                { "service:myservice:port", "5000" },
            });

            var configuration = builder.Build();

            var result = configuration.GetServiceUri("myservice", "metrics");
            Assert.Null(result);
        }
    }
}
