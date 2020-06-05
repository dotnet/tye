// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.DotNet.Watcher
{
    public class ProcessSpec
    {
        public string Executable { get; set; }
        public string WorkingDirectory { get; set; }
        public IDictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();
        public IEnumerable<string> Arguments { get; set; }
        public Action<string>? OutputData { get; set; }
        public Action<string>? ErrorData { get; set; }
        public Action<int>? OnStart { get; set; }
        public string ShortDisplayName()
            => Path.GetFileNameWithoutExtension(Executable);
    }
}
