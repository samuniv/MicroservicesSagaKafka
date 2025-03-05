using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using InventoryService.Events.IntegrationEvents;

namespace InventoryService.Infrastructure.MessageBus;

public class KafkaProducerService : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _inventoryTopic;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(
        IOptions<KafkaSettings> kafkaSettings,
        ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        _inventoryTopic = kafkaSettings.Value.InventoryTopic;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            EnableDeliveryReports = true,
            ClientId = "inventory-service-producer",
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000,
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => 
                _logger.LogError("Kafka error: {Reason}. Is Fatal: {IsFatal}", e.Reason, e.IsFatal))
            .Build();
    }

    public async Task PublishStockReservedEventAsync(StockReservedIntegrationEvent @event)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = @event.OrderId.ToString(),
                Value = JsonSerializer.Serialize(@event),
                Headers = new Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes(nameof(StockReservedIntegrationEvent)) }
                }
            };

            var deliveryResult = await _producer.ProduceAsync(_inventoryTopic, message);
            
            _logger.LogInformation(
                "Published StockReservedEvent: OrderId: {OrderId}, Status: {Status}, Partition: {Partition}, Offset: {Offset}",
                @event.OrderId,
                deliveryResult.Status,
                deliveryResult.Partition,
                deliveryResult.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing StockReservedEvent for OrderId: {OrderId}", @event.OrderId);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
} 