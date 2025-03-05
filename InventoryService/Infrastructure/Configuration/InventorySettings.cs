namespace InventoryService.Infrastructure.Configuration;

public class InventorySettings
{
    public StockThresholds Thresholds { get; set; } = new();
    public EventSettings Events { get; set; } = new();
}

public class StockThresholds
{
    public int WarningLevel { get; set; } = 10;
    public int CriticalLevel { get; set; } = 5;
    public int NormalLevel { get; set; } = 20;
    public bool EnableAutoReorder { get; set; } = true;
    public int ReorderPoint { get; set; } = 15;
    public int ReorderQuantity { get; set; } = 50;
}

public class EventSettings
{
    public bool PublishStockWarnings { get; set; } = true;
    public bool PublishStockNormalized { get; set; } = true;
    public bool PublishStockDepleted { get; set; } = true;
    public int EventRetryCount { get; set; } = 3;
    public int EventRetryDelayMs { get; set; } = 1000;
    public double EventRetryBackoffMultiplier { get; set; } = 2.0;
} 