using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PaymentService.Events;
using PaymentService.Infrastructure.Configuration;

namespace PaymentService.Infrastructure.MessageBus;

public class KafkaProducerService
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(
        IOptions<KafkaSettings> settings,
        ILogger<KafkaProducerService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            EnableDeliveryReports = true,
            RetryBackoffMs = _settings.RetrySettings.InitialRetryDelayMs,
            MessageTimeoutMs = _settings.RetrySettings.MaxRetryDelayMs,
            MessageSendMaxRetries = _settings.RetrySettings.MaxRetries
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishPaymentInitiatedEventAsync(PaymentInitiatedEvent @event)
    {
        await PublishEventAsync(@event, "payment-initiated");
    }

    public async Task PublishPaymentCompletedEventAsync(PaymentCompletedEvent @event)
    {
        await PublishEventAsync(@event, "payment-completed");
    }

    public async Task PublishPaymentFailedEventAsync(PaymentFailedEvent @event)
    {
        await PublishEventAsync(@event, "payment-failed");
    }

    public async Task PublishRefundInitiatedEventAsync(RefundInitiatedEvent @event)
    {
        await PublishEventAsync(@event, "refund-initiated");
    }

    private async Task PublishEventAsync<TEvent>(TEvent @event, string topic)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = System.Text.Json.JsonSerializer.Serialize(@event)
            };

            var deliveryResult = await _producer.ProduceAsync(
                _settings.PaymentsTopic + "-" + topic, 
                message);

            if (deliveryResult.Status == PersistenceStatus.Persisted)
            {
                _logger.LogInformation(
                    "Successfully published {EventType} to topic {Topic}", 
                    typeof(TEvent).Name, 
                    topic);
            }
            else
            {
                _logger.LogWarning(
                    "Message delivery for {EventType} to topic {Topic} reported status: {Status}", 
                    typeof(TEvent).Name, 
                    topic, 
                    deliveryResult.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish {EventType} to topic {Topic}", 
                typeof(TEvent).Name, 
                topic);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
} 