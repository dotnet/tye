// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Tye
{
    public class EnvironmentAttribute : Attribute
    {
        public EnvironmentAttribute(string environmentName)
        {
            if (environmentName is null)
            {
                throw new ArgumentNullException(nameof(environmentName));
            }

            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; }
    }
}
