﻿using System.Collections.Generic;

namespace Microsoft.Tye
{
    public class HttpProbeBuilder
    {
        public string Path { get; set; } = default!;
        public int Timeout { get; set; }
        public List<KeyValuePair<string, object>> Headers { get; set; } = new List<KeyValuePair<string, object>>();
    }
}
