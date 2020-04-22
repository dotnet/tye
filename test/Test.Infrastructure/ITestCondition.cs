<<<<<<< HEAD:test/E2ETest/Infrastructure/ITestCondition.cs
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace E2ETest
=======
﻿namespace Test.Infrastucture
>>>>>>> 491b1aa... Refactor PR:test/Test.Infrastructure/ITestCondition.cs
{
    public interface ITestCondition
    {
        bool IsMet { get; }

        string SkipReason { get; }
    }
}
