using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PaymentService.Infrastructure.Configuration;
using System.Text;

namespace PaymentService.Infrastructure.MessageBus;

public class KafkaConsumerService : IHostedService
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly KafkaProducerService _producer;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task _executingTask;

    public KafkaConsumerService(
        IOptions<KafkaSettings> settings,
        KafkaProducerService producer,
        ILogger<KafkaConsumerService> logger)
    {
        _settings = settings.Value;
        _producer = producer;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.ConsumerGroup,
            EnableAutoCommit = _settings.EnableAutoCommit,
            AutoCommitIntervalMs = _settings.AutoCommitIntervalMs,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SecurityProtocol = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol),
            SaslMechanism = Enum.Parse<SaslMechanism>(_settings.SaslMechanism)
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Consumer error: {Error}", e.Reason))
            .Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_settings.PaymentsTopic);
        _executingTask = Task.Run(() => ConsumeMessages(_cancellationTokenSource.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        if (_executingTask != null)
        {
            await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
        }
        _consumer?.Close();
    }

    private async Task ConsumeMessages(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(cancellationToken);
                if (consumeResult == null) continue;

                var retryCount = GetRetryCount(consumeResult.Message.Headers);
                
                try
                {
                    await ProcessMessageAsync(consumeResult);
                    _consumer.Commit(consumeResult);
                    _logger.LogInformation(
                        "Successfully processed message from topic {Topic} at offset {Offset}",
                        consumeResult.Topic, consumeResult.Offset);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing message from topic {Topic} at offset {Offset}",
                        consumeResult.Topic, consumeResult.Offset);

                    if (retryCount < _settings.MaxRetries)
                    {
                        await RetryMessageAsync(consumeResult, retryCount);
                    }
                    else if (_settings.EnableDlq)
                    {
                        await SendToDlqAsync(consumeResult, ex);
                        _consumer.Commit(consumeResult);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming messages");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult)
    {
        var messageType = GetMessageType(consumeResult.Message.Headers);
        _logger.LogInformation(
            "Processing message of type {MessageType} from topic {Topic}",
            messageType, consumeResult.Topic);

        // Add your message processing logic here
        // For example:
        // switch (messageType)
        // {
        //     case "OrderCreatedEvent":
        //         await ProcessOrderCreatedEvent(consumeResult.Message.Value);
        //         break;
        //     // Add more cases for other event types
        // }
    }

    private async Task RetryMessageAsync(ConsumeResult<string, string> consumeResult, int currentRetryCount)
    {
        var retryCount = currentRetryCount + 1;
        var headers = new Dictionary<string, string>
        {
            { "RetryCount", retryCount.ToString() },
            { "OriginalTopic", consumeResult.Topic },
            { "OriginalOffset", consumeResult.Offset.ToString() },
            { "RetryTimestamp", DateTime.UtcNow.ToString("O") }
        };

        await _producer.PublishAsync(
            consumeResult.Topic,
            consumeResult.Message.Key,
            consumeResult.Message.Value);

        _logger.LogInformation(
            "Message from topic {Topic} at offset {Offset} scheduled for retry {RetryCount}/{MaxRetries}",
            consumeResult.Topic, consumeResult.Offset, retryCount, _settings.MaxRetries);
    }

    private async Task SendToDlqAsync(ConsumeResult<string, string> consumeResult, Exception ex)
    {
        var dlqMessage = new DeadLetterMessage(
            consumeResult.Topic,
            consumeResult.Message.Key,
            consumeResult.Message.Value,
            ex.Message,
            GetRetryCount(consumeResult.Message.Headers),
            GetHeadersDictionary(consumeResult.Message.Headers),
            ex);

        await _producer.PublishAsync(
            _settings.DeadLetterTopic,
            consumeResult.Message.Key,
            dlqMessage);

        _logger.LogWarning(
            "Message from topic {Topic} at offset {Offset} moved to DLQ after {RetryCount} retries",
            consumeResult.Topic, consumeResult.Offset, _settings.MaxRetries);
    }

    private static int GetRetryCount(Headers headers)
    {
        var retryHeader = headers.FirstOrDefault(h => h.Key == "RetryCount");
        if (retryHeader == null) return 0;
        return int.Parse(Encoding.UTF8.GetString(retryHeader.GetValueBytes()));
    }

    private static string GetMessageType(Headers headers)
    {
        var typeHeader = headers.FirstOrDefault(h => h.Key == "MessageType");
        return typeHeader != null ? Encoding.UTF8.GetString(typeHeader.GetValueBytes()) : "Unknown";
    }

    private static Dictionary<string, string> GetHeadersDictionary(Headers headers)
    {
        return headers.ToDictionary(
            h => h.Key,
            h => Encoding.UTF8.GetString(h.GetValueBytes()));
    }
} 