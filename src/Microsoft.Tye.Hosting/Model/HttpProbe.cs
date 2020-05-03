using System.Collections.Generic;

namespace Microsoft.Tye.Hosting.Model
{
    public class HttpProbe
    {
        public string Path { get; set; } = default!;
        public List<KeyValuePair<string, object>> Headers { get; set; } = new List<KeyValuePair<string, object>>();
    }
}
