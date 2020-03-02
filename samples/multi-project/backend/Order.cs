using System;
using System.Runtime.Serialization;

namespace Shared
{
    [DataContract]
    public class Order
    {
        [DataMember(Order = 1)]
        public Guid OrderId { get; set; }

        [DataMember(Order = 2)]
        public string UserId { get; set; } = default!;

        [DataMember(Order = 3)]
        public DateTime CreatedTime { get; set; }
    }
}
