using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PaymentService.Infrastructure.Configuration;
using PaymentService.Models;
using System.Collections.Concurrent;

namespace PaymentService.Infrastructure.MessageBus;

public class DlqMonitoringService : IHostedService
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<DlqMonitoringService> _logger;
    private readonly ConcurrentDictionary<string, DeadLetterMessage> _messageCache;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task _executingTask;
    private readonly IConsumer<string, string> _consumer;

    public DlqMonitoringService(
        IOptions<KafkaSettings> settings,
        ILogger<DlqMonitoringService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _messageCache = new ConcurrentDictionary<string, DeadLetterMessage>();
        _cancellationTokenSource = new CancellationTokenSource();

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = $"{_settings.ConsumerGroup}-dlq-monitor",
            EnableAutoCommit = true,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SecurityProtocol = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol),
            SaslMechanism = Enum.Parse<SaslMechanism>(_settings.SaslMechanism)
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("DLQ Consumer error: {Error}", e.Reason))
            .Build();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_settings.DeadLetterTopic);
        _executingTask = Task.Run(() => MonitorDlqMessages(_cancellationTokenSource.Token));
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

    private async Task MonitorDlqMessages(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(cancellationToken);
                if (consumeResult == null) continue;

                var dlqMessage = DeadLetterMessage.FromJson(consumeResult.Message.Value);
                _messageCache.AddOrUpdate(
                    consumeResult.Message.Key,
                    dlqMessage,
                    (_, _) => dlqMessage);

                // Clean up old messages from cache (older than 24 hours)
                var oldMessages = _messageCache
                    .Where(kvp => kvp.Value.FailedAt < DateTime.UtcNow.AddHours(-24))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldMessages)
                {
                    _messageCache.TryRemove(key, out _);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring DLQ messages");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    public DlqStatistics GetStatistics()
    {
        var messages = _messageCache.Values.ToList();
        var now = DateTime.UtcNow;

        return new DlqStatistics
        {
            TotalMessages = messages.Count,
            LastHourMessages = messages.Count(m => m.FailedAt > now.AddHours(-1)),
            MessagesByTopic = messages
                .GroupBy(m => m.OriginalTopic)
                .ToDictionary(g => g.Key, g => g.Count()),
            MessagesByErrorType = messages
                .GroupBy(m => m.ErrorReason)
                .ToDictionary(g => g.Key, g => g.Count()),
            MessagesByRetryCount = messages
                .GroupBy(m => m.RetryCount)
                .ToDictionary(g => g.Key, g => g.Count()),
            LastMessageTimestamp = messages.Any() 
                ? messages.Max(m => m.FailedAt) 
                : DateTime.MinValue,
            RecentMessages = messages
                .OrderByDescending(m => m.FailedAt)
                .Take(10)
                .Select(m => new DlqMessageSummary
                {
                    MessageKey = m.MessageKey,
                    OriginalTopic = m.OriginalTopic,
                    ErrorReason = m.ErrorReason,
                    RetryCount = m.RetryCount,
                    FailedAt = m.FailedAt
                })
                .ToList()
        };
    }

    public async Task<bool> RetryMessageAsync(string messageKey)
    {
        if (!_messageCache.TryGetValue(messageKey, out var message))
        {
            return false;
        }

        try
        {
            using var producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                SecurityProtocol = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol),
                SaslMechanism = Enum.Parse<SaslMechanism>(_settings.SaslMechanism)
            }).Build();

            await producer.ProduceAsync(message.OriginalTopic, new Message<string, string>
            {
                Key = message.MessageKey,
                Value = message.MessageValue,
                Headers = new Headers(message.Headers.Select(h => 
                    new Header(h.Key, System.Text.Encoding.UTF8.GetBytes(h.Value))))
            });

            _messageCache.TryRemove(messageKey, out _);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry message {MessageKey}", messageKey);
            return false;
        }
    }

    public async Task<bool> RetryAllMessagesAsync()
    {
        var success = true;
        foreach (var key in _messageCache.Keys)
        {
            if (!await RetryMessageAsync(key))
            {
                success = false;
            }
        }
        return success;
    }
} 