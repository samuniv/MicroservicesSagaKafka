using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PaymentService.Events;
using PaymentService.Infrastructure.Configuration;

namespace PaymentService.Infrastructure.MessageBus;

public class KafkaProducerService : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaProducerService> _logger;
    private bool _disposed;

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
            RetryBackoffMs = _settings.RetryBackoffMs,
            MessageTimeoutMs = 45000, // 45 seconds
            RequestTimeoutMs = 30000, // 30 seconds
            SecurityProtocol = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol),
            SaslMechanism = Enum.Parse<SaslMechanism>(_settings.SaslMechanism)
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishPaymentInitiatedEventAsync(PaymentInitiatedEvent @event)
    {
        await PublishAsync("payment-initiated", Guid.NewGuid().ToString(), @event);
    }

    public async Task PublishPaymentCompletedEventAsync(PaymentCompletedEvent @event)
    {
        await PublishAsync("payment-completed", Guid.NewGuid().ToString(), @event);
    }

    public async Task PublishPaymentFailedEventAsync(PaymentFailedEvent @event)
    {
        await PublishAsync("payment-failed", Guid.NewGuid().ToString(), @event);
    }

    public async Task PublishRefundInitiatedEventAsync(RefundInitiatedEvent @event)
    {
        await PublishAsync("refund-initiated", Guid.NewGuid().ToString(), @event);
    }

    public async Task PublishAsync<T>(string topic, string key, T message)
    {
        try
        {
            var messageJson = System.Text.Json.JsonSerializer.Serialize(message);
            var deliveryResult = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = messageJson,
                Headers = new Headers
                {
                    { "MessageType", System.Text.Encoding.UTF8.GetBytes(typeof(T).Name) }
                }
            });

            _logger.LogInformation(
                "Message {MessageType} published to topic {Topic} with key {Key} at offset {Offset}",
                typeof(T).Name, topic, key, deliveryResult.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex,
                "Failed to publish message {MessageType} to topic {Topic} with key {Key}",
                typeof(T).Name, topic, key);

            if (_settings.EnableDlq)
            {
                await SendToDlqAsync(topic, key, message, ex);
            }

            throw;
        }
    }

    private async Task SendToDlqAsync<T>(string originalTopic, string key, T message, Exception exception)
    {
        try
        {
            var messageJson = System.Text.Json.JsonSerializer.Serialize(message);
            var dlqMessage = new DeadLetterMessage(
                originalTopic,
                key,
                messageJson,
                exception.Message,
                1,
                new Dictionary<string, string>
                {
                    { "MessageType", typeof(T).Name },
                    { "OriginalTimestamp", DateTime.UtcNow.ToString("O") }
                },
                exception);

            await _producer.ProduceAsync(_settings.DeadLetterTopic, new Message<string, string>
            {
                Key = key,
                Value = dlqMessage.ToJson(),
                Headers = new Headers
                {
                    { "MessageType", System.Text.Encoding.UTF8.GetBytes("DeadLetterMessage") }
                }
            });

            _logger.LogWarning(
                "Message sent to DLQ topic {Topic} with key {Key}",
                _settings.DeadLetterTopic, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send message to DLQ topic {Topic} with key {Key}",
                _settings.DeadLetterTopic, key);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _producer?.Flush(TimeSpan.FromSeconds(5));
            _producer?.Dispose();
        }

        _disposed = true;
    }
} 