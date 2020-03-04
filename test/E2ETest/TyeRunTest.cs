using System.Threading.Tasks;
using Tye.ConfigModel;
using Tye.Hosting;
using Xunit;

namespace E2ETest
{
    public class TyeRunTest
    {
        [Fact]
        public async Task BasicE2ETest()
        {
            var application = ConfigFactory.FromFile();
            TyeHost.RunAsync();
        }
    }
}
