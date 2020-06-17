using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Tye.Hosting.Model
{
    public class FunctionRunInfo : RunInfo
    {
        public FunctionRunInfo(FunctionServiceBuilder function)
        {
            Args = function.Args;
            FunctionPath = function.FunctionPath;
        }

        public string? Args { get; }
        public string FunctionPath { get; }
    }
}
