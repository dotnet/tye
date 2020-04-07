// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// Contains extension methods for adding Tye's secrets to <see cref="IConfiguration" />.
    /// </summary>
    public static class TyeSecretsConfigurationBuilderExtensions
    {
        /// <summary>
        /// Adds Tye's secrets to <see cref="IConfiguration" />.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder" />.</param>
        /// <returns>The <see cref="IConfigurationBuilder" />.</returns>
        /// <remarks>
        /// The environment variable <c>TYE_SECRETS_PATH</c> is used to populate the directory used by secrets.
        /// When the environment variable is specified, and the specified directory exists, then the value of
        /// <see cref="TyeSecretsConfigurationSource.FileProvider" /> will be non-null.
        /// </remarks>
        public static IConfigurationBuilder AddTyeSecrets(this IConfigurationBuilder builder)
        {
            var secretsDirectory = Environment.GetEnvironmentVariable(TyeSecretsConfigurationSource.TyeSecretsPathEnvironmentVariable);
            if (Directory.Exists(secretsDirectory))
            {
                foreach (var child in Directory.EnumerateDirectories(secretsDirectory))
                {
                    var source = new TyeSecretsConfigurationSource()
                    {
                        FileProvider = new PhysicalFileProvider(child),
                    };
                    builder.Add(source);
                }
            }

            return builder;
        }
    }
}
