// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;
using Xunit.Sdk;

namespace Test.Infrastructure
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Test.Infrastructure." + nameof(ConditionalFactDiscoverer), "Test.Infrastructure")]
    public class ConditionalFactAttribute : FactAttribute
    {
    }
}
