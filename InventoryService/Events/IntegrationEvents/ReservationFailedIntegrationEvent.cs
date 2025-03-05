namespace InventoryService.Events.IntegrationEvents;

public class ReservationFailedIntegrationEvent
{
    public Guid OrderId { get; }
    public List<FailedReservationItem> FailedItems { get; }
    public string Reason { get; }
    public DateTime FailedAt { get; }

    public ReservationFailedIntegrationEvent(
        Guid orderId,
        List<FailedReservationItem> failedItems,
        string reason)
    {
        OrderId = orderId;
        FailedItems = failedItems;
        Reason = reason;
        FailedAt = DateTime.UtcNow;
    }
}

public class FailedReservationItem
{
    public string ProductId { get; }
    public string ProductName { get; }
    public int RequestedQuantity { get; }
    public int AvailableQuantity { get; }

    public FailedReservationItem(
        string productId,
        string productName,
        int requestedQuantity,
        int availableQuantity)
    {
        ProductId = productId;
        ProductName = productName;
        RequestedQuantity = requestedQuantity;
        AvailableQuantity = availableQuantity;
    }
} 