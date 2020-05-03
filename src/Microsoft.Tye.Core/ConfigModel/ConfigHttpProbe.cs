using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;

namespace Microsoft.Tye.ConfigModel
{
    public class ConfigHttpProbe
    {
        [Required] public string Path { get; set; } = default!;
        public List<KeyValuePair<string, object>> Headers { get; set; } = new List<KeyValuePair<string, object>>();
    }
}
