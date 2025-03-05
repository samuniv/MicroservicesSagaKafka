namespace PaymentService.Events;

public class PaymentFailedEvent
{
    public Guid OrderId { get; set; }
    public string? TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string FailureReason { get; set; }
    public DateTime FailedAt { get; set; }

    public PaymentFailedEvent(Guid orderId, string? transactionId, decimal amount, string failureReason)
    {
        OrderId = orderId;
        TransactionId = transactionId;
        Amount = amount;
        FailureReason = failureReason;
        FailedAt = DateTime.UtcNow;
    }
} 