namespace InventoryService.Events.IntegrationEvents;

public class StockDepletedIntegrationEvent
{
    public string ProductId { get; }
    public string ProductName { get; }
    public int ReservedQuantity { get; }
    public DateTime DepletedAt { get; }
    public bool HasPendingReservations { get; }

    public StockDepletedIntegrationEvent(
        string productId,
        string productName,
        int reservedQuantity,
        bool hasPendingReservations)
    {
        ProductId = productId;
        ProductName = productName;
        ReservedQuantity = reservedQuantity;
        HasPendingReservations = hasPendingReservations;
        DepletedAt = DateTime.UtcNow;
    }
} 