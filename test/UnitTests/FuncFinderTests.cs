using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Tye.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Tye.UnitTests
{
    public class FuncFinderTests 
    {
        public FuncFinderTests(ITestOutputHelper output)
        {
            _logger = output.
        }

        [Fact]
        public async Task TestFunc()
        {
            var funcDownloader = new FuncDetector();
            await funcDownloader.PathToFunc("v2", "x64", downloadPath: null, logger: output, dryRun: true);
        }

        // Different arch
        // Different version
        // Different download path
        // make dry run work.
    }
}
