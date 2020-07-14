// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Testing
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class FrameworkSkipConditionAttribute : Attribute, ITestCondition
    {
        private readonly RuntimeFrameworks _excludedFrameworks;

        public FrameworkSkipConditionAttribute(RuntimeFrameworks excludedFrameworks)
        {
            _excludedFrameworks = excludedFrameworks;
        }

        public bool IsMet
        {
            get
            {
                return CanRunOnThisFramework(_excludedFrameworks);
            }
        }

        public string SkipReason { get; set; } = "Test cannot run on this runtime framework.";

        private static bool CanRunOnThisFramework(RuntimeFrameworks excludedFrameworks)
        {
            return true;
        }
    }
}
