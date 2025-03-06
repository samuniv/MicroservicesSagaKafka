using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryService.Infrastructure.MessageBus.DeadLetterQueue
{
    public class DlqHandler : BackgroundService
    {
        private readonly ILogger<DlqHandler> _logger;
        private readonly KafkaSettings _settings;
        private readonly IConsumer<string, string> _consumer;
        private readonly IProducer<string, string> _producer;

        public DlqHandler(
            ILogger<DlqHandler> logger,
            IOptions<KafkaSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                GroupId = $"{_settings.GroupId}-dlq",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                EnableIdempotence = true
            };

            _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            _producer = new ProducerBuilder<string, string>(producerConfig).Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _consumer.Subscribe(_settings.Topics.DeadLetterQueue);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(stoppingToken);
                        if (consumeResult == null) continue;

                        var failedMessage = JsonSerializer.Deserialize<FailedMessage>(consumeResult.Message.Value);
                        if (failedMessage == null) continue;

                        await HandleFailedMessageAsync(failedMessage);
                        
                        _consumer.Commit(consumeResult);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message from DLQ");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing DLQ message");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            finally
            {
                _consumer.Close();
            }
        }

        private async Task HandleFailedMessageAsync(FailedMessage failedMessage)
        {
            _logger.LogInformation(
                "Processing failed message: {MessageId}, Attempt: {RetryCount}, Original Topic: {OriginalTopic}",
                failedMessage.MessageId,
                failedMessage.RetryCount,
                failedMessage.OriginalTopic);

            if (failedMessage.RetryCount < _settings.MaxRetryAttempts)
            {
                await RetryMessageAsync(failedMessage);
            }
            else
            {
                await HandleMaxRetriesExceededAsync(failedMessage);
            }
        }

        private async Task RetryMessageAsync(FailedMessage failedMessage)
        {
            try
            {
                var retryMessage = new Message<string, string>
                {
                    Key = failedMessage.MessageId,
                    Value = failedMessage.OriginalMessage,
                    Headers = new Headers
                    {
                        { "RetryCount", BitConverter.GetBytes(failedMessage.RetryCount + 1) },
                        { "OriginalTopic", System.Text.Encoding.UTF8.GetBytes(failedMessage.OriginalTopic) }
                    }
                };

                await _producer.ProduceAsync(failedMessage.OriginalTopic, retryMessage);
                
                _logger.LogInformation(
                    "Message {MessageId} requeued to {Topic} for retry attempt {RetryCount}",
                    failedMessage.MessageId,
                    failedMessage.OriginalTopic,
                    failedMessage.RetryCount + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to retry message {MessageId} to topic {Topic}",
                    failedMessage.MessageId,
                    failedMessage.OriginalTopic);
                throw;
            }
        }

        private Task HandleMaxRetriesExceededAsync(FailedMessage failedMessage)
        {
            _logger.LogError(
                "Message {MessageId} has exceeded maximum retry attempts. Original Topic: {OriginalTopic}, Error: {Error}",
                failedMessage.MessageId,
                failedMessage.OriginalTopic,
                failedMessage.Error);

            // Here you could implement additional error handling:
            // - Store in error database
            // - Send notifications
            // - Trigger manual review process
            
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _consumer?.Dispose();
            _producer?.Dispose();
            base.Dispose();
        }
    }

    public class FailedMessage
    {
        public string MessageId { get; set; }
        public string OriginalTopic { get; set; }
        public string OriginalMessage { get; set; }
        public int RetryCount { get; set; }
        public string Error { get; set; }
        public DateTime FailedAt { get; set; }
    }
} 