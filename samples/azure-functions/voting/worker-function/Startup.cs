using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(worker_function.Startup))]

namespace worker_function
{
    public class Startup : FunctionsStartup
    {
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public override void Configure(IFunctionsHostBuilder builder)
        {
        }
    }
}