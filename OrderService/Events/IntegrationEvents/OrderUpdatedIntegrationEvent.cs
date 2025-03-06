using System;
using System.Collections.Generic;

namespace OrderService.Events.IntegrationEvents
{
    public class OrderUpdatedIntegrationEvent : OrderIntegrationEvent
    {
        public IEnumerable<OrderItemDto> UpdatedItems { get; }
        public decimal NewTotalAmount { get; }
        public string UpdateReason { get; }

        public OrderUpdatedIntegrationEvent(
            Guid orderId,
            IEnumerable<OrderItemDto> updatedItems,
            decimal newTotalAmount,
            string updateReason,
            string correlationId = null)
            : base(orderId, "OrderUpdated", correlationId)
        {
            UpdatedItems = updatedItems;
            NewTotalAmount = newTotalAmount;
            UpdateReason = updateReason;
        }
    }

    public class OrderItemDto
    {
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }
} 