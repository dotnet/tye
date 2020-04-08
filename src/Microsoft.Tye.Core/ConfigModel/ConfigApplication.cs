// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Tye;
using Tye.Serialization;
using YamlDotNet.Serialization;

namespace Microsoft.Tye.ConfigModel
{
    // General design note re: nullability - we use [Required] to validate for non-null
    // so we use = default! to reflect that. Code that creates a ConfigApplication should
    // validate it before dereferencing the properties.
    public class ConfigApplication
    {
        // This gets set by all of the code paths that read the application
        [YamlIgnore]
        public FileInfo Source { get; set; } = default!;

        public string? Name { get; set; }

        public string? Registry { get; set; }

        public string? Network { get; set; }

        public List<Dictionary<string, object>> Extensions { get; set; } = new List<Dictionary<string, object>>();

        public List<ConfigService> Services { get; set; } = new List<ConfigService>();

        public List<ConfigIngress> Ingress { get; set; } = new List<ConfigIngress>();

        public void Validate()
        {
            var config = this;

            var context = new ValidationContext(config);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(config, context, results, validateAllProperties: true))
            {
                throw new TyeYamlException(
                    "Configuration validation failed." + Environment.NewLine +
                    string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage)));
            }

            foreach (var extension in config.Extensions)
            {
                if (!extension.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name as string))
                {
                    throw new TyeYamlException(CoreStrings.ExtensionMustProvideAName);
                }
            }

            foreach (var service in config.Services)
            {
                context = new ValidationContext(service);
                if (!Validator.TryValidateObject(service, context, results, validateAllProperties: true))
                {
                    throw new TyeYamlException(
                        $"Service '{service.Name}' validation failed." + Environment.NewLine +
                        string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage)));
                }

                if (config.Services.Count(o => o.Name == service.Name) > 1)
                {
                    throw new TyeYamlException(CoreStrings.ServiceMustHaveUniqueNames);
                }

                if (service.Project != null && service.Image != null)
                {
                    throw new TyeYamlException(CoreStrings.FormatProjectImageExecutableExclusive("project", "image"));
                }

                if (service.Project != null && service.Executable != null)
                {
                    throw new TyeYamlException(CoreStrings.FormatProjectImageExecutableExclusive("project", "executable"));
                }

                if (service.Image != null && service.Executable != null)
                {
                    throw new TyeYamlException(CoreStrings.FormatProjectImageExecutableExclusive("image", "executable"));
                }

                foreach (var binding in service.Bindings)
                {
                    context = new ValidationContext(binding);
                    if (!Validator.TryValidateObject(binding, context, results, validateAllProperties: true))
                    {
                        throw new TyeYamlException(
                            $"Binding '{binding.Name}' of service '{service.Name}' validation failed." + Environment.NewLine +
                            string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage)));
                    }

                    if (string.IsNullOrEmpty(binding.Name) && service.Bindings.Count > 1)
                    {
                        throw new TyeYamlException(CoreStrings.FormatMultipleBindingWithoutName("service"));
                    }

                    if (service.Bindings.Count(o => o.Name == binding.Name) > 1)
                    {
                        throw new TyeYamlException(CoreStrings.FormatMultipleBindingWithSameName("service"));
                    }

                    if (service.Bindings.Count(o => o.Port == binding.Port) > 1)
                    {
                        throw new TyeYamlException(CoreStrings.FormatMultipleBindingWithSamePort("service"));
                    }
                }

                foreach (var envVar in service.Configuration)
                {
                    context = new ValidationContext(service);
                    if (!Validator.TryValidateObject(service, context, results, validateAllProperties: true))
                    {
                        throw new TyeYamlException(
                            $"Environment variable '{envVar.Name}' of service '{service.Name}' validation failed." + Environment.NewLine +
                            string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage)));
                    }
                }

                foreach (var volume in service.Volumes)
                {
                    context = new ValidationContext(service);
                    if (!Validator.TryValidateObject(service, context, results, validateAllProperties: true))
                    {
                        throw new TyeYamlException(
                            $"Volume '{volume.Source}' of service '{service.Name}' validation failed." + Environment.NewLine +
                            string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage)));
                    }
                }
            }

            foreach (var ingress in config.Ingress)
            {
                context = new ValidationContext(ingress);
                if (!Validator.TryValidateObject(ingress, context, results, validateAllProperties: true))
                {
                    throw new TyeYamlException(
                        $"Ingress '{ingress.Name}' validation failed." + Environment.NewLine +
                        string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage)));
                }

                foreach (var binding in ingress.Bindings)
                {
                    context = new ValidationContext(binding);
                    if (!Validator.TryValidateObject(binding, context, results, validateAllProperties: true))
                    {
                        throw new TyeYamlException(
                            $"Binding '{binding.Name}' of ingress '{ingress.Name}' validation failed." + Environment.NewLine +
                            string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage)));
                    }
                    if (string.IsNullOrEmpty(binding.Name) && ingress.Bindings.Count > 1)
                    {
                        throw new TyeYamlException(CoreStrings.FormatMultipleBindingWithoutName("ingress"));
                    }
                    if (ingress.Bindings.Count(o => o.Name == binding.Name) > 1)
                    {
                        throw new TyeYamlException(CoreStrings.FormatMultipleBindingWithSameName("ingress"));
                    }
                    if (binding.Protocol != "http" && binding.Protocol != "https" && binding.Protocol != null)
                    {
                        throw new TyeYamlException(CoreStrings.IngressBindingMustBeHttpOrHttps);
                    }
                    if (ingress.Bindings.Count(o => o.Port == binding.Port) > 1)
                    {
                        throw new TyeYamlException(CoreStrings.FormatMultipleBindingWithSamePort("ingress"));
                    }
                }

                // Make sure all ingress rules have an associated service
                foreach (var rule in ingress.Rules)
                {
                    context = new ValidationContext(rule);
                    if (!Validator.TryValidateObject(rule, context, results, validateAllProperties: true))
                    {
                        throw new TyeYamlException(
                            $"Rule '{rule.Path}' of ingress '{ingress.Name}' validation failed." + Environment.NewLine +
                            string.Join(Environment.NewLine, results.Select(r => r.ErrorMessage)));
                    }

                    if (config.Services.Count(o => o.Name == rule.Service) != 1)
                    {
                        throw new TyeYamlException(CoreStrings.IngressRuleMustReferenceService);
                    }
                }
            }
        }
    }
}
