using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using InventoryService.Events.IntegrationEvents;
using InventoryService.Infrastructure.Configuration;

namespace InventoryService.Infrastructure.MessageBus;

public class KafkaProducerService : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _inventoryTopic;
    private readonly ILogger<KafkaProducerService> _logger;
    private readonly EventPublishRetryPolicy _retryPolicy;
    private readonly InventorySettings _settings;

    public KafkaProducerService(
        IOptions<KafkaSettings> kafkaSettings,
        IOptions<InventorySettings> inventorySettings,
        EventPublishRetryPolicy retryPolicy,
        ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        _retryPolicy = retryPolicy;
        _settings = inventorySettings.Value;
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
        await PublishEventAsync(@event, @event.OrderId.ToString());
    }

    public async Task PublishStockLevelWarningEventAsync(StockLevelWarningIntegrationEvent @event)
    {
        if (_settings.Events.PublishStockWarnings)
        {
            await PublishEventAsync(@event, @event.ProductId);
        }
    }

    public async Task PublishStockLevelNormalizedEventAsync(StockLevelNormalizedIntegrationEvent @event)
    {
        if (_settings.Events.PublishStockNormalized)
        {
            await PublishEventAsync(@event, @event.ProductId);
        }
    }

    public async Task PublishStockDepletedEventAsync(StockDepletedIntegrationEvent @event)
    {
        if (_settings.Events.PublishStockDepleted)
        {
            await PublishEventAsync(@event, @event.ProductId);
        }
    }

    public async Task PublishReservationFailedEventAsync(ReservationFailedIntegrationEvent @event)
    {
        await PublishEventAsync(@event, @event.OrderId.ToString());
    }

    private async Task PublishEventAsync<TEvent>(TEvent @event, string messageKey) where TEvent : class
    {
        var eventName = typeof(TEvent).Name;

        await _retryPolicy.ExecuteAsync(async () =>
        {
            var message = new Message<string, string>
            {
                Key = messageKey,
                Value = JsonSerializer.Serialize(@event),
                Headers = new Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes(eventName) }
                }
            };

            var deliveryResult = await _producer.ProduceAsync(_inventoryTopic, message);
            
            _logger.LogInformation(
                "Published {EventType}: Key: {Key}, Status: {Status}, Partition: {Partition}, Offset: {Offset}",
                eventName,
                messageKey,
                deliveryResult.Status,
                deliveryResult.Partition,
                deliveryResult.Offset);
        }, $"Publish {eventName}");
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
} 