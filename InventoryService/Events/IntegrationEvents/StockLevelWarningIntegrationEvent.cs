namespace InventoryService.Events.IntegrationEvents;

public class StockLevelWarningIntegrationEvent
{
    public string ProductId { get; }
    public string ProductName { get; }
    public int CurrentQuantity { get; }
    public int WarningThreshold { get; }
    public DateTime DetectedAt { get; }

    public StockLevelWarningIntegrationEvent(
        string productId,
        string productName,
        int currentQuantity,
        int warningThreshold)
    {
        ProductId = productId;
        ProductName = productName;
        CurrentQuantity = currentQuantity;
        WarningThreshold = warningThreshold;
        DetectedAt = DateTime.UtcNow;
    }
} 