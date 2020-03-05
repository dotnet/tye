// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Tye
{
    internal static class Names
    {
        public static string NormalizeToDns(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            name = name.ToLowerInvariant();
            name = name.Replace('.', '-');
            name = name.Replace('_', '-');
            return name;
        }

        public static string NormalizeToFriendly(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            name = name.Replace('.', '_');
            name = name.Replace('-', '_');
            return name;
        }
    }
}
