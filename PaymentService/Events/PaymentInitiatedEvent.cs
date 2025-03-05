namespace PaymentService.Events;

public class PaymentInitiatedEvent
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime InitiatedAt { get; set; }

    public PaymentInitiatedEvent(Guid orderId, decimal amount)
    {
        OrderId = orderId;
        Amount = amount;
        InitiatedAt = DateTime.UtcNow;
    }
} 