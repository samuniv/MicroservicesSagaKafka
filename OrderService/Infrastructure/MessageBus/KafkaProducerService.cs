using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using OrderService.Events.IntegrationEvents;

namespace OrderService.Infrastructure.MessageBus;

public class KafkaProducerService : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _ordersTopic;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(
        IOptions<KafkaSettings> kafkaSettings,
        ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        _ordersTopic = kafkaSettings.Value.OrdersTopic;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            EnableDeliveryReports = true,
            ClientId = "order-service",
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000,
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishOrderEventAsync<T>(T orderEvent) where T : OrderIntegrationEvent
    {
        try
        {
            var eventName = typeof(T).Name;
            var key = orderEvent.OrderId.ToString();
            var value = JsonSerializer.Serialize(orderEvent);

            _logger.LogInformation(
                "Publishing {EventName} to {Topic}. OrderId: {OrderId}, CustomerId: {CustomerId}",
                eventName, _ordersTopic, orderEvent.OrderId, orderEvent.CustomerId);

            var message = new Message<string, string>
            {
                Key = key,
                Value = value,
                Headers = new Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes(eventName) }
                }
            };

            var deliveryResult = await _producer.ProduceAsync(_ordersTopic, message);

            if (deliveryResult.Status == PersistenceStatus.Persisted)
            {
                _logger.LogInformation(
                    "{EventName} published successfully. OrderId: {OrderId}, Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                    eventName, orderEvent.OrderId, deliveryResult.Topic, deliveryResult.Partition, deliveryResult.Offset);
            }
            else
            {
                _logger.LogWarning(
                    "Message delivery reported non-persistent status. OrderId: {OrderId}, Status: {Status}",
                    orderEvent.OrderId, deliveryResult.Status);
            }
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex,
                "Error publishing {EventName}. OrderId: {OrderId}, Error: {Error}",
                typeof(T).Name, orderEvent.OrderId, ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
} 