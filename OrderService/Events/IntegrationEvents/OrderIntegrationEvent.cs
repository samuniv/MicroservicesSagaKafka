namespace OrderService.Events.IntegrationEvents;

public abstract class OrderIntegrationEvent
{
    public Guid Id { get; }
    public DateTime Timestamp { get; }
    public Guid OrderId { get; }
    public string CustomerId { get; }
    public decimal TotalAmount { get; }

    protected OrderIntegrationEvent(Guid orderId, string customerId, decimal totalAmount)
    {
        Id = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
    }
} 