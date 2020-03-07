// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Tye
{
    public class CommandException : Exception
    {
        public CommandException(string message)
            : base(message)
        { }

        public CommandException(string message, Exception inner)
        : base(message, inner)
        { }
    }
}
