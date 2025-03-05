namespace InventoryService.Events.IntegrationEvents;

public class StockReservedIntegrationEvent
{
    public Guid OrderId { get; }
    public string ProductId { get; }
    public int Quantity { get; }
    public decimal UnitPrice { get; }
    public DateTime ReservedAt { get; }

    public StockReservedIntegrationEvent(Guid orderId, string productId, int quantity, decimal unitPrice)
    {
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        ReservedAt = DateTime.UtcNow;
    }
} 