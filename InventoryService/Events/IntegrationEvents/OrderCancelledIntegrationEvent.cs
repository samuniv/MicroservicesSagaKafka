namespace InventoryService.Events.IntegrationEvents;

public class OrderCancelledIntegrationEvent
{
    public Guid OrderId { get; }
    public string Reason { get; }
    public List<OrderItem> Items { get; }
    public DateTime CancelledAt { get; }

    public OrderCancelledIntegrationEvent(Guid orderId, string reason, List<OrderItem> items)
    {
        OrderId = orderId;
        Reason = reason;
        Items = items;
        CancelledAt = DateTime.UtcNow;
    }
} 