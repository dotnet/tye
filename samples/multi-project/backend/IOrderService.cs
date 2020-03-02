using System.ServiceModel;
using System.Threading.Tasks;

namespace Shared
{
    [ServiceContract]
    public interface IOrderService
    {
        ValueTask PlaceOrderAsync(Order order);
    }
}
