using System;

namespace InventoryService.Events.IntegrationEvents
{
    public class InventoryReleasedEvent : BaseIntegrationEvent
    {
        public Guid OrderId { get; }
        public Guid ProductId { get; }
        public int Quantity { get; }

        public InventoryReleasedEvent(
            Guid orderId,
            Guid productId,
            int quantity,
            string correlationId = null) 
            : base("InventoryReleased", correlationId)
        {
            OrderId = orderId;
            ProductId = productId;
            Quantity = quantity;
        }
    }
} 