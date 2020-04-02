// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration.KeyPerFile;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// An <see cref="IConfigurationSource" /> implementation for Tye's secrets.
    /// </summary>
    public sealed class TyeSecretsConfigurationSource : IConfigurationSource
    {
        /// <summary>
        /// The environment variable used to configure the path where Tye looks for secrets.
        /// </summary>
        public static readonly string TyeSecretsPathEnvironmentVariable = "TYE_SECRETS_PATH";

        /// <summary>
        /// Gets or sets the <see cref="IFileProvider" /> used by the configuration source.
        /// </summary>
        public IFileProvider? FileProvider { get; set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            var source = new KeyPerFileConfigurationSource()
            {
                FileProvider = FileProvider,
                Optional = true,
            };

            return source.Build(builder);

        }
    }
}
