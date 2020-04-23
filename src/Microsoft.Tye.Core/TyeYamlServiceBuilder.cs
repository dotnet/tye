// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Tye
{
    public class TyeYamlServiceBuilder : ServiceBuilder
    {
        public ApplicationBuilder Builder { get; }

        public TyeYamlServiceBuilder(string name, ApplicationBuilder builder) : base(name)
        {
            Builder = builder;
        }
    }
}
