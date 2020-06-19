﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Tye
{
    public class ProcessSpec
    {
        public string? Executable { get; set; }
        public string? WorkingDirectory { get; set; }
        public IDictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();
        public string? Arguments { get; set; }
        public Action<string>? OutputData { get; set; }
        public Func<Task<int>>? Build { get; set; }
        public Action<string>? ErrorData { get; set; }
        public Action<int>? OnStart { get; set; }
        public Action<int>? OnStop { get; set; }
        public string? ShortDisplayName()
            => Path.GetFileNameWithoutExtension(Executable);
    }
}
