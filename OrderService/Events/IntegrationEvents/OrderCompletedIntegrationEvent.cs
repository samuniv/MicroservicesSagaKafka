using System;

namespace OrderService.Events.IntegrationEvents
{
    public class OrderCompletedIntegrationEvent : OrderIntegrationEvent
    {
        public DateTime CompletedAt { get; }
        public decimal FinalAmount { get; }
        public string PaymentTransactionId { get; }

        public OrderCompletedIntegrationEvent(
            Guid orderId,
            DateTime completedAt,
            decimal finalAmount,
            string paymentTransactionId,
            string correlationId = null)
            : base(orderId, "OrderCompleted", correlationId)
        {
            CompletedAt = completedAt;
            FinalAmount = finalAmount;
            PaymentTransactionId = paymentTransactionId;
        }
    }
} 