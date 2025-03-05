using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using InventoryService.Services;
using InventoryService.Events.IntegrationEvents;

namespace InventoryService.Infrastructure.MessageBus;

public class OrderEventsConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _ordersTopic;
    private readonly ILogger<OrderEventsConsumer> _logger;

    public OrderEventsConsumer(
        IOptions<KafkaSettings> kafkaSettings,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderEventsConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _ordersTopic = kafkaSettings.Value.OrdersTopic;

        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = kafkaSettings.Value.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            MaxPollIntervalMs = 300000, // 5 minutes
            SessionTimeoutMs = 30000, // 30 seconds
            HeartbeatIntervalMs = 3000, // 3 seconds
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => 
                _logger.LogError("Kafka error: {Reason}. Is Fatal: {IsFatal}", e.Reason, e.IsFatal))
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                _logger.LogInformation("Assigned partitions: {Partitions}", 
                    string.Join(", ", partitions.Select(p => p.Partition.Value)));
            })
            .SetPartitionsRevokedHandler((c, partitions) =>
            {
                _logger.LogInformation("Revoked partitions: {Partitions}", 
                    string.Join(", ", partitions.Select(p => p.Partition.Value)));
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // Ensure we don't block the startup path

        try
        {
            _consumer.Subscribe(_ordersTopic);
            _logger.LogInformation("Started listening for order events on topic: {Topic}", _ordersTopic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(stoppingToken);
                    if (consumeResult == null) continue;

                    var eventType = GetEventType(consumeResult.Message.Headers);
                    _logger.LogInformation(
                        "Received message: Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, EventType: {EventType}",
                        consumeResult.Topic,
                        consumeResult.Partition,
                        consumeResult.Offset,
                        eventType);

                    await ProcessMessageAsync(consumeResult.Message, eventType);
                    _consumer.Commit(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in consumer loop");
        }
        finally
        {
            _consumer.Close();
        }
    }

    private string GetEventType(Headers headers)
    {
        var eventTypeHeader = headers.FirstOrDefault(h => h.Key == "event-type");
        return eventTypeHeader != null 
            ? System.Text.Encoding.UTF8.GetString(eventTypeHeader.GetValueBytes()) 
            : "Unknown";
    }

    private async Task ProcessMessageAsync(Message<string, string> message, string eventType)
    {
        using var scope = _scopeFactory.CreateScope();
        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        try
        {
            switch (eventType)
            {
                case nameof(OrderCreatedIntegrationEvent):
                    var orderCreatedEvent = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(message.Value);
                    if (orderCreatedEvent != null)
                    {
                        await ProcessOrderCreatedEventAsync(orderCreatedEvent, inventoryService);
                    }
                    break;

                case nameof(OrderCancelledIntegrationEvent):
                    var orderCancelledEvent = JsonSerializer.Deserialize<OrderCancelledIntegrationEvent>(message.Value);
                    if (orderCancelledEvent != null)
                    {
                        await ProcessOrderCancelledEventAsync(orderCancelledEvent, inventoryService);
                    }
                    break;

                default:
                    _logger.LogWarning("Unknown event type: {EventType}", eventType);
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing message of type {EventType}", eventType);
            throw;
        }
    }

    private async Task ProcessOrderCreatedEventAsync(
        OrderCreatedIntegrationEvent orderCreatedEvent,
        IInventoryService inventoryService)
    {
        _logger.LogInformation("Processing OrderCreatedEvent: OrderId: {OrderId}", orderCreatedEvent.OrderId);

        foreach (var item in orderCreatedEvent.Items)
        {
            try
            {
                var success = await inventoryService.ReserveStockAsync(item.ProductId, item.Quantity);
                if (!success)
                {
                    _logger.LogWarning(
                        "Failed to reserve stock for OrderId: {OrderId}, ProductId: {ProductId}, Quantity: {Quantity}",
                        orderCreatedEvent.OrderId,
                        item.ProductId,
                        item.Quantity);
                    // Here you might want to publish an event back to the order service
                    // to indicate that the reservation failed
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error reserving stock for OrderId: {OrderId}, ProductId: {ProductId}, Quantity: {Quantity}",
                    orderCreatedEvent.OrderId,
                    item.ProductId,
                    item.Quantity);
                throw;
            }
        }
    }

    private async Task ProcessOrderCancelledEventAsync(
        OrderCancelledIntegrationEvent orderCancelledEvent,
        IInventoryService inventoryService)
    {
        _logger.LogInformation(
            "Processing OrderCancelledEvent: OrderId: {OrderId}, Reason: {Reason}",
            orderCancelledEvent.OrderId,
            orderCancelledEvent.Reason);

        foreach (var item in orderCancelledEvent.Items)
        {
            try
            {
                await inventoryService.CancelReservationAsync(item.ProductId, item.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error cancelling reservation for OrderId: {OrderId}, ProductId: {ProductId}, Quantity: {Quantity}",
                    orderCancelledEvent.OrderId,
                    item.ProductId,
                    item.Quantity);
                throw;
            }
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
} 