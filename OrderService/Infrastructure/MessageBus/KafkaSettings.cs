namespace OrderService.Infrastructure.MessageBus;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string OrdersTopic { get; set; } = "orders";
} 