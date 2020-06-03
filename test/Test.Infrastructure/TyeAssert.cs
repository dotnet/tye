﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.Tye.ConfigModel;
using Xunit;

namespace Test.Infrastructure
{
    public static class TyeAssert
    {
        public static void Equal(ConfigApplication expected, ConfigApplication actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Registry, actual.Registry);
            Assert.Equal(expected.Network, actual.Network);

            foreach (var ingress in actual.Ingress)
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
                        .Where(o => o.Path == rule.Path && o.Host == rule.Host && o.Service?.Equals(rule.Service, StringComparison.OrdinalIgnoreCase) == true)
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
                Assert.Equal(otherIngress.Tags, ingress.Tags);
            }

            foreach (var service in actual.Services)
            {
                var otherService = expected
                    .Services
                    .Where(o => o.Name.Equals(service.Name, StringComparison.OrdinalIgnoreCase))
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
                Assert.Equal(otherService.Tags, service.Tags);

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
    }
}
