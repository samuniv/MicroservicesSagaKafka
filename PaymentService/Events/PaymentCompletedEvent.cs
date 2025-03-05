namespace PaymentService.Events;

public class PaymentCompletedEvent
{
    public Guid OrderId { get; set; }
    public string TransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CompletedAt { get; set; }

    public PaymentCompletedEvent(Guid orderId, string transactionId, decimal amount)
    {
        OrderId = orderId;
        TransactionId = transactionId;
        Amount = amount;
        CompletedAt = DateTime.UtcNow;
    }
} 