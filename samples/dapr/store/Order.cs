using System;

namespace store
{
    public class Order
    {
        public string OrderId { get; set; } = default!;
        public int ProductId { get; set ; }
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