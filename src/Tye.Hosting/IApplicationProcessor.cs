using System.Threading.Tasks;
using Tye.Hosting.Model;

namespace Tye.Hosting
{
    public interface IApplicationProcessor
    {
        Task StartAsync(Application application);

        Task StopAsync(Application application);
    }
}
