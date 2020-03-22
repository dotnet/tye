using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigIngressServiceBinding
    {
        public string? Name { get; set; }
        public bool AutoAssignPort { get; set; }
        public int? Port { get; set; }
        public string? Protocol { get; set; } // HTTP or HTTPS
    }
}
