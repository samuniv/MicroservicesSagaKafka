namespace InventoryService.Infrastructure.MessageBus;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string OrdersTopic { get; set; } = string.Empty;
    public string InventoryTopic { get; set; } = string.Empty;
} 