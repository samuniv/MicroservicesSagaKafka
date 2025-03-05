using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderService.Events.IntegrationEvents;

namespace OrderService.Infrastructure.MessageBus;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly string _ordersTopic;
    private readonly RetryPolicy _retryPolicy;
    private readonly int _maxRetryAttempts;
    private readonly Dictionary<string, int> _messageRetryCount;
    private readonly TimeSpan _commitPeriod;
    private DateTime _lastCommitTime;

    public KafkaConsumerService(
        IOptions<KafkaSettings> kafkaSettings,
        ILogger<KafkaConsumerService> logger)
    {
        _logger = logger;
        _ordersTopic = kafkaSettings.Value.OrdersTopic;
        _retryPolicy = new RetryPolicy(logger);
        _maxRetryAttempts = 3;
        _messageRetryCount = new Dictionary<string, int>();
        _commitPeriod = TimeSpan.FromSeconds(5);
        _lastCommitTime = DateTime.UtcNow;

        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers,
            GroupId = "order-service-test-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            // Add error handling and retry settings
            MaxPollIntervalMs = 300000, // 5 minutes
            SessionTimeoutMs = 30000, // 30 seconds
            HeartbeatIntervalMs = 3000, // 3 seconds
            FetchMaxBytes = 52428800, // 50 MB
            MessageMaxBytes = 10485760 // 10 MB
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => 
                _logger.LogError("Kafka error: {Reason}. Is Fatal: {IsFatal}", e.Reason, e.IsFatal))
            .SetStatisticsHandler((_, json) => 
                _logger.LogTrace("Kafka statistics: {Statistics}", json))
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                _logger.LogInformation("Partitions assigned: {Partitions}", 
                    string.Join(", ", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
            })
            .SetPartitionsRevokedHandler((c, partitions) =>
            {
                _logger.LogInformation("Partitions revoked: {Partitions}", 
                    string.Join(", ", partitions.Select(p => $"{p.Topic}:{p.Partition}")));
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SubscribeToTopicWithRetry(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = await _retryPolicy.ExecuteAsync(
                        () => Task.FromResult(_consumer.Consume(TimeSpan.FromSeconds(1))),
                        "Consume message");

                    if (consumeResult == null) continue;

                    var messageKey = $"{consumeResult.Topic}:{consumeResult.Partition}:{consumeResult.Offset}";
                    
                    try
                    {
                        var eventType = GetEventType(consumeResult.Message.Headers);
                        _logger.LogInformation(
                            "Received message: Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, Key: {Key}, EventType: {EventType}",
                            consumeResult.Topic,
                            consumeResult.Partition,
                            consumeResult.Offset,
                            consumeResult.Message.Key,
                            eventType);

                        // Process the message with retry policy
                        await _retryPolicy.ExecuteAsync(
                            () => ProcessMessage(consumeResult.Message, eventType),
                            $"Process {eventType} message");

                        // Remove from retry tracking if successful
                        _messageRetryCount.Remove(messageKey);

                        // Commit offsets periodically
                        if (ShouldCommitOffsets())
                        {
                            await CommitOffsetsWithRetry(consumeResult);
                        }
                    }
                    catch (Exception ex)
                    {
                        await HandleMessageProcessingError(messageKey, consumeResult, ex);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message");
                    await Task.Delay(1000, stoppingToken); // Add delay before retry
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumer is shutting down");
        }
        finally
        {
            await CommitFinalOffsetsWithRetry();
            _consumer.Close();
        }
    }

    private async Task SubscribeToTopicWithRetry(CancellationToken stoppingToken)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            _consumer.Subscribe(_ordersTopic);
            await Task.CompletedTask;
        }, "Subscribe to topic");
    }

    private bool ShouldCommitOffsets()
    {
        var now = DateTime.UtcNow;
        if (now - _lastCommitTime >= _commitPeriod)
        {
            _lastCommitTime = now;
            return true;
        }
        return false;
    }

    private async Task CommitOffsetsWithRetry(ConsumeResult<string, string> consumeResult)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            _consumer.Commit(consumeResult);
            _logger.LogInformation(
                "Committed offset {Offset} for partition {Partition}",
                consumeResult.Offset,
                consumeResult.Partition);
            await Task.CompletedTask;
        }, "Commit offsets");
    }

    private async Task CommitFinalOffsetsWithRetry()
    {
        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                _consumer.Commit();
                _logger.LogInformation("Final offsets committed");
                await Task.CompletedTask;
            }, "Commit final offsets");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing final offsets");
        }
    }

    private async Task HandleMessageProcessingError(
        string messageKey,
        ConsumeResult<string, string> consumeResult,
        Exception error)
    {
        if (!_messageRetryCount.TryGetValue(messageKey, out int retryCount))
        {
            retryCount = 0;
        }

        retryCount++;
        _messageRetryCount[messageKey] = retryCount;

        if (retryCount >= _maxRetryAttempts)
        {
            _logger.LogError(
                error,
                "Message processing failed after {RetryCount} attempts. Moving to dead letter queue. Key: {Key}",
                retryCount,
                messageKey);

            await MoveToDeadLetterQueue(consumeResult);
            _messageRetryCount.Remove(messageKey);
        }
        else
        {
            _logger.LogWarning(
                error,
                "Message processing failed. Attempt {RetryCount} of {MaxRetryAttempts}. Key: {Key}",
                retryCount,
                _maxRetryAttempts,
                messageKey);
        }
    }

    private async Task MoveToDeadLetterQueue(ConsumeResult<string, string> consumeResult)
    {
        try
        {
            // Here you would typically:
            // 1. Publish the failed message to a dead letter queue topic
            // 2. Store additional metadata about the failure
            // 3. Potentially notify monitoring systems
            
            _logger.LogInformation(
                "Message moved to dead letter queue: Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                consumeResult.Topic,
                consumeResult.Partition,
                consumeResult.Offset);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving message to dead letter queue");
        }
    }

    private string GetEventType(Headers headers)
    {
        var eventTypeHeader = headers.FirstOrDefault(h => h.Key == "event-type");
        return eventTypeHeader != null 
            ? System.Text.Encoding.UTF8.GetString(eventTypeHeader.GetValueBytes()) 
            : "Unknown";
    }

    private async Task ProcessMessage(Message<string, string> message, string eventType)
    {
        try
        {
            switch (eventType)
            {
                case nameof(OrderCreatedIntegrationEvent):
                    var orderCreatedEvent = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(message.Value);
                    _logger.LogInformation("Processing OrderCreatedEvent: OrderId: {OrderId}, CustomerId: {CustomerId}, TotalAmount: {TotalAmount}",
                        orderCreatedEvent?.OrderId,
                        orderCreatedEvent?.CustomerId,
                        orderCreatedEvent?.TotalAmount);
                    break;

                case nameof(OrderStatusChangedIntegrationEvent):
                    var statusChangedEvent = JsonSerializer.Deserialize<OrderStatusChangedIntegrationEvent>(message.Value);
                    _logger.LogInformation("Processing OrderStatusChangedEvent: OrderId: {OrderId}, OldStatus: {OldStatus}, NewStatus: {NewStatus}",
                        statusChangedEvent?.OrderId,
                        statusChangedEvent?.OldStatus,
                        statusChangedEvent?.NewStatus);
                    break;

                case nameof(OrderCancelledIntegrationEvent):
                    var cancelledEvent = JsonSerializer.Deserialize<OrderCancelledIntegrationEvent>(message.Value);
                    _logger.LogInformation("Processing OrderCancelledEvent: OrderId: {OrderId}, Reason: {Reason}",
                        cancelledEvent?.OrderId,
                        cancelledEvent?.Reason);
                    break;

                default:
                    _logger.LogWarning("Unknown event type: {EventType}", eventType);
                    break;
            }

            await Task.CompletedTask;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error deserializing message of type {EventType}", eventType);
            throw;
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
} 