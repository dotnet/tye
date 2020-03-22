using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Hosting.Model
{
    public class IngressRunInfo : RunInfo
    {
        public IngressRunInfo(List<IngressRule> rules)
        {
            Rules = rules;
        }

        public List<IngressRule> Rules { get; }
    }
}
