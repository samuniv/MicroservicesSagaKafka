namespace InventoryService.Events.IntegrationEvents;

public class StockLevelNormalizedIntegrationEvent
{
    public string ProductId { get; }
    public string ProductName { get; }
    public int CurrentQuantity { get; }
    public int NormalThreshold { get; }
    public DateTime NormalizedAt { get; }

    public StockLevelNormalizedIntegrationEvent(
        string productId,
        string productName,
        int currentQuantity,
        int normalThreshold)
    {
        ProductId = productId;
        ProductName = productName;
        CurrentQuantity = currentQuantity;
        NormalThreshold = normalThreshold;
        NormalizedAt = DateTime.UtcNow;
    }
} 