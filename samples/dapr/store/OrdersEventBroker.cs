using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace store
{
    public class OrdersEventBroker
    {
        private ConcurrentDictionary<string, Entry> _entries;
        private ILogger<OrdersEventBroker> _logger;

        public OrdersEventBroker(ILogger<OrdersEventBroker> logger)
        {
            _entries = new ConcurrentDictionary<string, Entry>();
            _logger = logger;
        }

        public async Task<OrderConfirmation> GetOrderConfirmationAsync(string orderId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Waiting for confirmation of order {OrderId}", orderId);

            var entry = new Entry(orderId);
            using (cancellationToken.Register(Cancel))
            {
                _entries.TryAdd(orderId, entry);

                var state = await entry.Completion.Task;
                _entries.TryRemove(orderId, out _);

                _logger.LogInformation("Order {OrderId} has been processed", orderId);
                return state;
            }

            void Cancel()
            {
                _logger.LogInformation("Canceling subscription {OrderId}", orderId);
                _entries.TryRemove(orderId, out _);
                entry.Completion.TrySetCanceled(cancellationToken);
            }
        }

        public void Complete(OrderConfirmation result)
        {
            if (_entries.TryGetValue(result.OrderId, out var entry))
            {
                _logger.LogInformation("Processing order {OrderId}", result.OrderId);
                entry.Completion.TrySetResult(result);
            }
        }

        private class Entry
        {
            public Entry(string orderId)
            {
                OrderId = orderId;

                Completion = new TaskCompletionSource<OrderConfirmation>();
            }

            public string OrderId { get; }

            public TaskCompletionSource<OrderConfirmation> Completion { get; }
        }
    }
}