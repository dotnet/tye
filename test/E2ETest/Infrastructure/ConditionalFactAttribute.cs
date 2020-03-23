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
