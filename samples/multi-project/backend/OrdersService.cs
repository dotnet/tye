using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared;

namespace Backend
{
    public class OrdersService : IOrderService
    {
        private readonly ILogger<OrdersService> _logger;
        private readonly IModel _client;

        public OrdersService(ILogger<OrdersService> logger, IModel client)
        {
            _logger = logger;
            _client = client;
        }

        public ValueTask PlaceOrderAsync(Order order)
        {
            var orderBytes = JsonSerializer.SerializeToUtf8Bytes(order);

            // Use the raw log API to avoid formatting the JSON string (since it has {})
            var text = Encoding.UTF8.GetString(orderBytes);
            _logger.Log(LogLevel.Information, 0, "Recieved: " + text, exception: null, formatter: (m, e) => m);

            _client.BasicPublish(
                exchange: "",
                routingKey: "orders",
                basicProperties: null,
                body: orderBytes);

            return default;
        }
    }
}
