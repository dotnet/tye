// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;
using Xunit.Sdk;

namespace E2ETest
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("E2ETest." + nameof(ConditionalFactDiscoverer), "Microsoft.Tye.E2ETest")]
    public class ConditionalFactAttribute : FactAttribute
    {
    }
}
