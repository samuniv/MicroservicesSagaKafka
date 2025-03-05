namespace InventoryService.Events.IntegrationEvents;

public class OrderCreatedIntegrationEvent
{
    public Guid OrderId { get; }
    public string CustomerId { get; }
    public List<OrderItem> Items { get; }
    public DateTime CreatedAt { get; }

    public OrderCreatedIntegrationEvent(Guid orderId, string customerId, List<OrderItem> items)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Items = items;
        CreatedAt = DateTime.UtcNow;
    }
}

public class OrderItem
{
    public string ProductId { get; }
    public int Quantity { get; }
    public decimal UnitPrice { get; }

    public OrderItem(string productId, int quantity, decimal unitPrice)
    {
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
} 