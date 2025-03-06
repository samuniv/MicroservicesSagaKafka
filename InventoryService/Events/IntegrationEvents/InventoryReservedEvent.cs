using System;

namespace InventoryService.Events.IntegrationEvents
{
    public class InventoryReservedEvent : BaseIntegrationEvent
    {
        public Guid OrderId { get; }
        public Guid ProductId { get; }
        public int Quantity { get; }
        public bool Success { get; }

        public InventoryReservedEvent(
            Guid orderId,
            Guid productId,
            int quantity,
            bool success,
            string correlationId = null) 
            : base("InventoryReserved", correlationId)
        {
            OrderId = orderId;
            ProductId = productId;
            Quantity = quantity;
            Success = success;
        }
    }
} 