using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigHttpProber
    {
        [Required] public string Path { get; set; } = default!;
        public int? Port { get; set; }
        public string? Protocol { get; set; }
        public List<KeyValuePair<string, object>> Headers { get; set; } = new List<KeyValuePair<string, object>>();
    }
}
