﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.DotNet.Watcher
{
    public interface IConsole
    {
        event ConsoleCancelEventHandler CancelKeyPress;
        TextWriter Out { get; }
        TextWriter Error { get; }
        TextReader In { get; }
        bool IsInputRedirected { get; }
        bool IsOutputRedirected { get; }
        bool IsErrorRedirected { get; }
        ConsoleColor ForegroundColor { get; set; }
        void ResetColor();
    }
}
