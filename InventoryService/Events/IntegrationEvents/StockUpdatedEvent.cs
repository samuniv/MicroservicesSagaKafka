using System;

namespace InventoryService.Events.IntegrationEvents
{
    public class StockUpdatedEvent : BaseIntegrationEvent
    {
        public Guid ProductId { get; }
        public int NewQuantity { get; }
        public int Reserved { get; }
        public int Available { get; }

        public StockUpdatedEvent(
            Guid productId,
            int newQuantity,
            int reserved,
            string correlationId = null) 
            : base("StockUpdated", correlationId)
        {
            ProductId = productId;
            NewQuantity = newQuantity;
            Reserved = reserved;
            Available = newQuantity - reserved;
        }
    }
} 