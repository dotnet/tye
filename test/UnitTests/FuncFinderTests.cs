using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing;
using Microsoft.Tye.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Tye.UnitTests
{
    public class FuncFinderTests : LoggedTest 
    {

        [Fact]
        public async Task TestFunc()
        {
            var funcDownloader = new FuncDetector();
            var res = await funcDownloader.PathToFunc("v2", "x64", downloadPath: null, logger: LoggerFactory.CreateLogger("AzureFunctionTest"), default, dryRun: true);
        }

        // Different arch
        // Different version
        // Different download path
        // make dry run work.
    }
}
