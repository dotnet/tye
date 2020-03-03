using System.Threading.Tasks;
using Micronetes.Hosting.Model;

namespace Micronetes.Hosting
{
    public interface IApplicationProcessor
    {
        Task StartAsync(Application application);

        Task StopAsync(Application application);
    }
}
