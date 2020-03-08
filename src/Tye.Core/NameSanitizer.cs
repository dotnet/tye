// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Tye
{
    public static class NameSanitizer
    {
        // Converts an arbitrary string into something usable as a C# identifier.
        //
        // For instance the project file might be `test-project.csproj` but `test-project` is
        // not what will appear in `launchSettings.json`. We need to convert to
        // `test_project`
        public static string SanitizeToIdentifier(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            // This is not perfect. For now it just handles cases we've encountered.
            return name.Replace("-", "_");
        }
    }
}
