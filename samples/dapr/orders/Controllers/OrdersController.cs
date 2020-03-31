using System;
using System.Threading.Tasks;
using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace orders.Controllers
{
    [ApiController]
    public class OrdersController : ControllerBase
    {
        [Topic("orderplaced")]
        [HttpPost("orderplaced")]
        public async Task PlaceOrder(Order order, [FromServices] DaprClient dapr, [FromServices] ILogger<OrdersController> logger)
        {
            logger.LogInformation("Got order {OrderId} for product {ProductId}", order.OrderId, order.ProductId);

            var state = await dapr.GetStateEntryAsync<InventoryState>("default", order.ProductId.ToString());
            if (state.Value == null || state.Value.Remaining < -10)
            {
                // For demo purposes, assume we have 5 of these in stock :)
                state.Value = new InventoryState() { Remaining = 5, }; 
            }

            state.Value.Remaining--;
            await state.SaveAsync();

            logger.LogInformation("Updated inventory for product {ProductId} to {Inventory}", order.ProductId, state.Value.Remaining);

            OrderConfirmation confirmation;
            if (state.Value.Remaining >= 0)
            {
                confirmation = new OrderConfirmation()
                {
                    OrderId = order.OrderId,
                    Confirmed = true,
                    DeliveryDate = DateTime.Now.AddYears(1),
                    RemainingCount = state.Value.Remaining,
                };
            }
            else
            {
                confirmation = new OrderConfirmation()
                {
                    OrderId = order.OrderId,
                    Confirmed = false,
                    BackorderCount = -1 * state.Value.Remaining,
                };
            }

            await dapr.PublishEventAsync("orderprocessed", confirmation);

            logger.LogInformation("Sent confirmation for order {OrderId}", order.OrderId);

        }
    }

    public class Order
    {
        public string OrderId { get; set; } = default!;
        public int ProductId { get; set ; }
    }

    public class InventoryState
    {
        public int Remaining { get; set; }
    }

    public class OrderConfirmation
    {
        public string OrderId { get; set; } = default!;

        public DateTime? DeliveryDate { get; set; }

        public bool Confirmed { get; set; }

        public int BackorderCount { get; set; }

        public int RemainingCount { get; set; }
    }
}
