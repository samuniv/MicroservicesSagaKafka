using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace InventoryService.Infrastructure.HealthChecks
{
    public class KafkaHealthCheck : IHealthCheck
    {
        private readonly IProducer<string, string> _producer;
        private readonly string _bootstrapServers;
        private readonly string _topic;

        public KafkaHealthCheck(
            IProducer<string, string> producer,
            IOptions<KafkaSettings> settings)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
            _bootstrapServers = settings.Value.BootstrapServers;
            _topic = settings.Value.Topics.InventoryEvents;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var config = new AdminClientConfig { BootstrapServers = _bootstrapServers };
                using var adminClient = new AdminClientBuilder(config).Build();

                // Check broker connectivity
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                if (metadata.Brokers.Count == 0)
                {
                    return HealthCheckResult.Unhealthy("No Kafka brokers available");
                }

                // Check topic availability
                var topics = adminClient.GetMetadata(_topic, TimeSpan.FromSeconds(5));
                if (!topics.Topics.Exists(t => t.Topic == _topic))
                {
                    return HealthCheckResult.Unhealthy($"Topic '{_topic}' not found");
                }

                // Check producer connectivity by sending a test message
                var testMessage = new Message<string, string>
                {
                    Key = "health-check",
                    Value = "ping"
                };

                await _producer.ProduceAsync(_topic, testMessage, cancellationToken);

                return HealthCheckResult.Healthy("Kafka connection is healthy");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Kafka connection failed", ex);
            }
        }
    }
} 