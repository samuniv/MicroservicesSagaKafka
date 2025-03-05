using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PaymentService.Infrastructure.Configuration;

namespace PaymentService.Infrastructure.MessageBus;

public class DlqCleanupService : BackgroundService
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<DlqCleanupService> _logger;
    private readonly IAdminClient _adminClient;
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7); // Keep messages for 7 days
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1); // Run cleanup every hour

    public DlqCleanupService(
        IOptions<KafkaSettings> settings,
        ILogger<DlqCleanupService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var config = new AdminClientConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            SecurityProtocol = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol),
            SaslMechanism = Enum.Parse<SaslMechanism>(_settings.SaslMechanism)
        };

        _adminClient = new AdminClientBuilder(config).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldMessages();
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during DLQ cleanup");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait 5 minutes before retrying on error
            }
        }
    }

    private async Task CleanupOldMessages()
    {
        try
        {
            var metadata = _adminClient.GetMetadata(_settings.DeadLetterTopic, TimeSpan.FromSeconds(10));
            var topic = metadata.Topics.FirstOrDefault(t => t.Topic == _settings.DeadLetterTopic);
            
            if (topic == null)
            {
                _logger.LogWarning("DLQ topic {Topic} not found", _settings.DeadLetterTopic);
                return;
            }

            using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                GroupId = $"{_settings.ConsumerGroup}-cleanup",
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                SecurityProtocol = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol),
                SaslMechanism = Enum.Parse<SaslMechanism>(_settings.SaslMechanism)
            }).Build();

            var cutoffTime = DateTime.UtcNow.Subtract(_retentionPeriod);
            var partitions = topic.Partitions.Select(p => new TopicPartition(_settings.DeadLetterTopic, p.PartitionId));
            consumer.Assign(partitions);

            var deletedCount = 0;
            foreach (var partition in partitions)
            {
                try
                {
                    var watermarkOffsets = consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(5));
                    if (watermarkOffsets.High.Value == 0) continue;

                    consumer.Seek(new TopicPartitionOffset(partition, watermarkOffsets.Low));
                    
                    while (true)
                    {
                        var consumeResult = consumer.Consume(TimeSpan.FromSeconds(1));
                        if (consumeResult == null) break;

                        var dlqMessage = DeadLetterMessage.FromJson(consumeResult.Message.Value);
                        if (dlqMessage.FailedAt < cutoffTime)
                        {
                            deletedCount++;
                            // Mark message for deletion by committing offset
                            consumer.Commit(consumeResult);
                        }
                        else
                        {
                            // We've reached messages that are still within retention period
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning up partition {Partition}", partition);
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Cleaned up {Count} messages older than {Cutoff} from DLQ",
                    deletedCount, cutoffTime);
            }
        }
        finally
        {
            _adminClient.Dispose();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _adminClient?.Dispose();
        await base.StopAsync(cancellationToken);
    }
} 