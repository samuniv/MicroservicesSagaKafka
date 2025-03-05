namespace PaymentService.Events;

public class RefundInitiatedEvent
{
    public Guid OrderId { get; set; }
    public string TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; }
    public DateTime InitiatedAt { get; set; }

    public RefundInitiatedEvent(Guid orderId, string transactionId, decimal amount, string reason)
    {
        OrderId = orderId;
        TransactionId = transactionId;
        Amount = amount;
        Reason = reason;
        InitiatedAt = DateTime.UtcNow;
    }
} 