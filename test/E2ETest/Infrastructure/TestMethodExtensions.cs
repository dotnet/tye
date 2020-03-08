using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace E2ETest
{
    public static class TestMethodExtensions
    {
        public static string EvaluateSkipConditions(this ITestMethod testMethod)
        {
            var testClass = testMethod.TestClass.Class;
            var assembly = testMethod.TestClass.TestCollection.TestAssembly.Assembly;
            var conditionAttributes = testMethod.Method
                .GetCustomAttributes(typeof(ITestCondition))
                .Concat(testClass.GetCustomAttributes(typeof(ITestCondition)))
                .Concat(assembly.GetCustomAttributes(typeof(ITestCondition)))
                .OfType<ReflectionAttributeInfo>()
                .Select(attributeInfo => attributeInfo.Attribute);

            foreach (ITestCondition condition in conditionAttributes)
            {
                if (!condition.IsMet)
                {
                    return condition.SkipReason;
                }
            }

            return null!;
        }
    }
}
